using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Events.Dtos;
using SmartParking.Domain.Entities;

namespace SmartParking.Application.Events;

/// <summary>
/// Service xử lý trung tâm luồng sự kiện IoT/Staff.
///
/// Luồng nghiệp vụ Sprint 3 (Free Parking Model – thực tế Việt Nam):
///   Camera cổng vào chụp ảnh → Gemini 2.5 Pro phân tích → Staff cổng xác nhận
///     → [VehicleEntered] → Tạo phiên pending_slot
///
///   Cảm biến siêu âm ô A1-012 phát hiện có vật thể
///     → [SensorTriggered] → Ghi log, chưa gán phiên (chờ Staff)
///
///   Staff khu vực đến xác nhận, bấm gán xe vào ô trên mobile
///     → [SlotParked] → Gán slot, phiên chuyển sang parked
///     → Hệ thống tự kiểm tra tương thích nhiên liệu (xăng/điện) → cảnh báo nếu sai
///
///   Xe ra cổng, Staff đối chiếu ảnh Gemini, thu tiền (Phụng xử lý)
///     → [VehicleExited] → Đóng phiên
/// </summary>
public class ParkingEventService
{
    private readonly IParkingEventLogRepository _eventLogRepo;
    private readonly IParkingSessionRepository  _sessionRepo;

    public ParkingEventService(
        IParkingEventLogRepository eventLogRepo,
        IParkingSessionRepository  sessionRepo)
    {
        _eventLogRepo = eventLogRepo;
        _sessionRepo  = sessionRepo;
    }

    public async Task<IngestEventResponse> IngestAsync(
        IngestEventRequest request, CancellationToken ct = default)
    {
        // ── 1. IDEMPOTENCY CHECK ─────────────────────────────────────────────
        // Gửi lặp cùng EventId → trả kết quả cũ, không xử lý lần 2.
        var existingLog = await _eventLogRepo.FindByEventIdAsync(request.EventId, ct);
        if (existingLog is not null)
        {
            return new IngestEventResponse
            {
                Success          = true,
                IsDuplicate      = true,
                ProcessingStatus = EventProcessingStatus.Duplicate,
                EventId          = request.EventId,
                SessionId        = existingLog.SessionId,
                Message          = "Sự kiện đã được xử lý trước đó (idempotent – bỏ qua)."
            };
        }

        // ── 2. DISPATCH THEO LOẠI SỰ KIỆN ───────────────────────────────────
        return request.EventType switch
        {
            ParkingEventType.VehicleEntered  => await HandleVehicleEnteredAsync(request, ct),
            ParkingEventType.SensorTriggered => await HandleSensorTriggeredAsync(request, ct),
            ParkingEventType.SlotParked      => await HandleSlotParkedAsync(request, ct),
            ParkingEventType.VehicleExited   => await HandleVehicleExitedAsync(request, ct),
            _ => await RejectAndLogAsync(request,
                     $"EventType '{request.EventType}' không được hỗ trợ.", ct)
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HANDLER: VehicleEntered – Xe qua cổng vào
    // ─────────────────────────────────────────────────────────────────────────
    private async Task<IngestEventResponse> HandleVehicleEnteredAsync(
        IngestEventRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.CarPlate))
            return await RejectAndLogAsync(request, "CarPlate bắt buộc với VehicleEntered.", ct);

        // Chặn xe đang đỗ check-in lại
        var existing = await _sessionRepo.FindActiveByCarPlateAsync(
            request.CarPlate.Trim().ToUpperInvariant(), ct);

        if (existing is not null)
        {
            return await RejectAndLogAsync(request,
                $"Biển số '{request.CarPlate}' đang có phiên mở (SessionId: {existing.SessionId}). " +
                "Xe phải ra khỏi bãi trước khi check-in lại.", ct);
        }

        var sessionId = string.IsNullOrWhiteSpace(request.SessionId)
            ? Guid.NewGuid().ToString()
            : request.SessionId;

        var session = new ParkingSession
        {
            SessionId     = sessionId,
            CarPlate      = request.CarPlate.Trim().ToUpperInvariant(),
            ParkingLotId  = request.ParkingLotId,
            Status        = ParkingSessionStatus.PendingSlot,
            EntryImageUrl = request.EntryImageUrl,
            FuelType      = request.FuelType,
            CarColor      = request.CarColor,
            CarModel      = request.CarModel,
            GateStaffId   = request.TriggeredBy,
            EnteredAtUtc  = request.EventTimestampUtc ?? DateTimeOffset.UtcNow
        };

        await _sessionRepo.AddAsync(session, ct);
        await WriteLogAsync(request, sessionId, EventProcessingStatus.Accepted, null, ct);

        return new IngestEventResponse
        {
            Success          = true,
            ProcessingStatus = EventProcessingStatus.Accepted,
            EventId          = request.EventId,
            SessionId        = sessionId,
            SessionStatus    = ParkingSessionStatus.PendingSlot,
            Message          = $"Xe '{request.CarPlate}' đã vào cổng. Phiên {sessionId} đang chờ gán slot."
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HANDLER: SensorTriggered – Cảm biến siêu âm tại ô báo có xe
    // ─────────────────────────────────────────────────────────────────────────
    private async Task<IngestEventResponse> HandleSensorTriggeredAsync(
        IngestEventRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.SlotCode))
            return await RejectAndLogAsync(request, "SlotCode bắt buộc với SensorTriggered.", ct);

        // Chỉ ghi log – Staff khu vực sẽ gán thủ công qua SlotParked
        await WriteLogAsync(request, request.SessionId, EventProcessingStatus.Accepted, null, ct);

        return new IngestEventResponse
        {
            Success          = true,
            ProcessingStatus = EventProcessingStatus.Accepted,
            EventId          = request.EventId,
            SessionId        = request.SessionId,
            Message          = $"Cảm biến ô '{request.SlotCode}' phát hiện có xe. Chờ Staff khu vực xác nhận."
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HANDLER: SlotParked – Staff khu vực gán xe vào ô
    // ─────────────────────────────────────────────────────────────────────────
    private async Task<IngestEventResponse> HandleSlotParkedAsync(
        IngestEventRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId))
            return await RejectAndLogAsync(request, "SessionId bắt buộc với SlotParked.", ct);
        if (string.IsNullOrWhiteSpace(request.SlotCode))
            return await RejectAndLogAsync(request, "SlotCode bắt buộc với SlotParked.", ct);

        var session = await _sessionRepo.FindBySessionIdAsync(request.SessionId, ct);
        if (session is null)
            return await RejectAndLogAsync(request,
                $"Không tìm thấy phiên '{request.SessionId}'.", ct);

        if (session.Status != ParkingSessionStatus.PendingSlot)
            return await RejectAndLogAsync(request,
                $"Phiên '{request.SessionId}' đang ở trạng thái '{session.Status}' – không thể gán slot.", ct);

        string? warningCode    = null;
        string? warningMessage = null;

        // Gán slot, chuyển phiên sang parked
        session.SlotCode    = request.SlotCode;
        session.Status      = ParkingSessionStatus.Parked;
        session.WarningCode = warningCode;
        session.ZoneStaffId = request.TriggeredBy;
        session.ParkedAtUtc = request.EventTimestampUtc ?? DateTimeOffset.UtcNow;

        await _sessionRepo.UpdateAsync(session, ct);
        await WriteLogAsync(request, request.SessionId, EventProcessingStatus.Accepted, null, ct);

        return new IngestEventResponse
        {
            Success          = true,
            ProcessingStatus = EventProcessingStatus.Accepted,
            EventId          = request.EventId,
            SessionId        = request.SessionId,
            SessionStatus    = ParkingSessionStatus.Parked,
            WarningCode      = warningCode,
            WarningMessage   = warningMessage,
            Message          = $"Xe '{session.CarPlate}' đã đậu tại ô '{request.SlotCode}'."
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HANDLER: VehicleExited – Xe ra cổng
    // ─────────────────────────────────────────────────────────────────────────
    private async Task<IngestEventResponse> HandleVehicleExitedAsync(
        IngestEventRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId))
            return await RejectAndLogAsync(request, "SessionId bắt buộc với VehicleExited.", ct);

        var session = await _sessionRepo.FindBySessionIdAsync(request.SessionId, ct);
        if (session is null)
            return await RejectAndLogAsync(request,
                $"Không tìm thấy phiên '{request.SessionId}'.", ct);

        if (session.Status == ParkingSessionStatus.Exited)
            return await RejectAndLogAsync(request,
                $"Phiên '{request.SessionId}' đã đóng – xe không thể ra lần hai.", ct);

        // Đóng phiên. Thu tiền và giải phóng slot do API của Phụng xử lý riêng.
        session.Status       = ParkingSessionStatus.Exited;
        session.ExitImageUrl = request.ExitImageUrl;
        session.ExitedAtUtc  = request.EventTimestampUtc ?? DateTimeOffset.UtcNow;

        await _sessionRepo.UpdateAsync(session, ct);
        await WriteLogAsync(request, request.SessionId, EventProcessingStatus.Accepted, null, ct);

        return new IngestEventResponse
        {
            Success          = true,
            ProcessingStatus = EventProcessingStatus.Accepted,
            EventId          = request.EventId,
            SessionId        = request.SessionId,
            SessionStatus    = ParkingSessionStatus.Exited,
            Message          = $"Xe '{session.CarPlate}' đã ra khỏi bãi. Phiên '{request.SessionId}' đã đóng."
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────────────────
    private async Task<IngestEventResponse> RejectAndLogAsync(
        IngestEventRequest request, string reason, CancellationToken ct)
    {
        await WriteLogAsync(request, request.SessionId, EventProcessingStatus.Rejected, reason, ct);
        return new IngestEventResponse
        {
            Success          = false,
            ProcessingStatus = EventProcessingStatus.Rejected,
            EventId          = request.EventId,
            SessionId        = request.SessionId,
            Message          = reason
        };
    }

    private Task WriteLogAsync(
        IngestEventRequest request,
        string?            sessionId,
        string             status,
        string?            rejectReason,
        CancellationToken  ct)
    {
        return _eventLogRepo.AddAsync(new ParkingEventLog
        {
            EventId           = request.EventId,
            SessionId         = sessionId,
            ParkingLotId      = request.ParkingLotId,
            SlotCode          = request.SlotCode,
            CarPlate          = request.CarPlate ?? string.Empty,
            EventType         = request.EventType,
            Source            = request.Source,
            TriggeredBy       = request.TriggeredBy,
            ProcessingStatus  = status,
            RejectReason      = rejectReason,
            EventTimestampUtc = request.EventTimestampUtc ?? DateTimeOffset.UtcNow,
            ReceivedAtUtc     = DateTimeOffset.UtcNow
        }, ct);
    }
}
