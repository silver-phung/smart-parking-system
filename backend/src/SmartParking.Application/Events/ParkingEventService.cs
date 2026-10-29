using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Events.Dtos;
using SmartParking.Domain.Entities;

namespace SmartParking.Application.Events;

/// <summary>
/// Ingestion pipeline cho bằng chứng camera/cảm biến và thao tác Staff.
/// Pipeline giữ projection riêng để theo dõi; trạng thái core chỉ được đổi qua API nghiệp vụ.
/// OCR tại cổng là dữ liệu tạm, còn VehicleExited chỉ mở bước xử lý cổng ra.
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
        IngestEventRequest request,
        string? authenticatedActorId = null,
        CancellationToken ct = default)
    {
        if (!Guid.TryParseExact(request.EventId, "D", out var eventId) ||
            eventId.Version != 4)
        {
            return new IngestEventResponse
            {
                Success = false,
                ProcessingStatus = EventProcessingStatus.Rejected,
                EventId = request.EventId,
                Message = "EventId phải là UUID v4 hợp lệ."
            };
        }

        if (request.PayloadVersion != 1)
            return await RejectAndLogAsync(request, authenticatedActorId,
                $"PayloadVersion '{request.PayloadVersion}' chưa được hỗ trợ.", ct);

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
            ParkingEventType.VehicleEntered  => await HandleVehicleEnteredAsync(request, authenticatedActorId, ct),
            ParkingEventType.SensorTriggered => await HandleSensorTriggeredAsync(request, authenticatedActorId, ct),
            ParkingEventType.SlotParked      => await HandleSlotParkedAsync(request, authenticatedActorId, ct),
            ParkingEventType.VehicleExited   => await HandleVehicleExitedAsync(request, authenticatedActorId, ct),
            _ => await RejectAndLogAsync(request, authenticatedActorId,
                     $"EventType '{request.EventType}' không được hỗ trợ.", ct)
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HANDLER: VehicleEntered – Xe qua cổng vào
    // ─────────────────────────────────────────────────────────────────────────
    private async Task<IngestEventResponse> HandleVehicleEnteredAsync(
        IngestEventRequest request, string? authenticatedActorId, CancellationToken ct)
    {
        var sessionId = string.IsNullOrWhiteSpace(request.SessionId)
            ? Guid.NewGuid().ToString()
            : request.SessionId;

        var session = new ParkingSession
        {
            SessionId     = sessionId,
            CarPlate      = string.Empty,
            RecognizedPlate = NormalizePlate(request.CarPlate),
            ParkingLotId  = request.ParkingLotId,
            Status        = ParkingSessionStatus.WaitingVerification,
            EntryImageUrl = request.EntryImageUrl,
            RecognizedFuelType = request.FuelType,
            CarColor      = request.CarColor,
            CarModel      = request.CarModel,
            EnteredAtUtc  = request.EventTimestampUtc ?? DateTimeOffset.UtcNow
        };

        await _sessionRepo.AddAsync(session, ct);
        await WriteLogAsync(request, sessionId, authenticatedActorId, EventProcessingStatus.Accepted, null, ct);

        return new IngestEventResponse
        {
            Success          = true,
            ProcessingStatus = EventProcessingStatus.Accepted,
            EventId          = request.EventId,
            SessionId        = sessionId,
            SessionStatus    = ParkingSessionStatus.WaitingVerification,
            Message          = $"Đã nhận bằng chứng cổng vào cho phiên {sessionId}; OCR đang chờ Staff điều phối xác minh tại slot."
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HANDLER: SensorTriggered – Cảm biến siêu âm tại ô báo có xe
    // ─────────────────────────────────────────────────────────────────────────
    private async Task<IngestEventResponse> HandleSensorTriggeredAsync(
        IngestEventRequest request, string? authenticatedActorId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.SlotCode))
            return await RejectAndLogAsync(request, authenticatedActorId, "SlotCode bắt buộc với SensorTriggered.", ct);

        // Chỉ ghi log – Staff khu vực sẽ gán thủ công qua SlotParked
        await WriteLogAsync(request, request.SessionId, authenticatedActorId, EventProcessingStatus.Accepted, null, ct);

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
        IngestEventRequest request, string? authenticatedActorId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId))
            return await RejectAndLogAsync(request, authenticatedActorId, "SessionId bắt buộc với SlotParked.", ct);
        if (string.IsNullOrWhiteSpace(request.SlotCode))
            return await RejectAndLogAsync(request, authenticatedActorId, "SlotCode bắt buộc với SlotParked.", ct);
        if (string.IsNullOrWhiteSpace(authenticatedActorId))
            return await RejectAndLogAsync(request, null,
                "SlotParked cần danh tính Staff điều phối từ ngữ cảnh xác thực.", ct);
        if (string.IsNullOrWhiteSpace(request.CarPlate))
            return await RejectAndLogAsync(request, authenticatedActorId,
                "Staff điều phối phải xác nhận biển số tại actual slot.", ct);

        var session = await _sessionRepo.FindBySessionIdAsync(request.SessionId, ct);
        if (session is null)
            return await RejectAndLogAsync(request, authenticatedActorId,
                $"Không tìm thấy phiên '{request.SessionId}'.", ct);

        if (session.Status != ParkingSessionStatus.WaitingVerification)
            return await RejectAndLogAsync(request, authenticatedActorId,
                $"Phiên '{request.SessionId}' đang ở trạng thái '{session.Status}' – không thể xác nhận slot.", ct);

        var confirmedPlate = NormalizePlate(request.CarPlate)!;
        var existing = await _sessionRepo.FindActiveByCarPlateAsync(confirmedPlate, ct);
        if (existing is not null && !existing.SessionId.Equals(session.SessionId, StringComparison.OrdinalIgnoreCase))
            return await RejectAndLogAsync(request, authenticatedActorId,
                $"Biển số '{confirmedPlate}' đã có phiên hoạt động khác.", ct);

        // Đây là xác minh thực tế; slot physical chỉ được render OCCUPIED sau bước này.
        session.CarPlate    = confirmedPlate;
        session.FuelType    = request.FuelType;
        session.SlotCode    = request.SlotCode;
        session.Status      = ParkingSessionStatus.Parked;
        session.ZoneStaffId = authenticatedActorId;
        session.ParkedAtUtc = request.EventTimestampUtc ?? DateTimeOffset.UtcNow;

        await _sessionRepo.UpdateAsync(session, ct);
        await WriteLogAsync(request, request.SessionId, authenticatedActorId, EventProcessingStatus.Accepted, null, ct);

        return new IngestEventResponse
        {
            Success          = true,
            ProcessingStatus = EventProcessingStatus.Accepted,
            EventId          = request.EventId,
            SessionId        = request.SessionId,
            SessionStatus    = ParkingSessionStatus.Parked,
            Message          = $"Xe '{session.CarPlate}' đã đậu tại ô '{request.SlotCode}'."
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HANDLER: VehicleExited – Xe ra cổng
    // ─────────────────────────────────────────────────────────────────────────
    private async Task<IngestEventResponse> HandleVehicleExitedAsync(
        IngestEventRequest request, string? authenticatedActorId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId))
            return await RejectAndLogAsync(request, authenticatedActorId, "SessionId bắt buộc với VehicleExited.", ct);

        var session = await _sessionRepo.FindBySessionIdAsync(request.SessionId, ct);
        if (session is null)
            return await RejectAndLogAsync(request, authenticatedActorId,
                $"Không tìm thấy phiên '{request.SessionId}'.", ct);

        if (session.Status != ParkingSessionStatus.Parked)
            return await RejectAndLogAsync(request, authenticatedActorId,
                $"Phiên '{request.SessionId}' đang ở trạng thái '{session.Status}' – không thể mở xử lý cổng ra.", ct);

        // Chỉ ghi nhận xe tới cổng ra. API phí/vé/confirm-exit mới được đóng phiên và trả ô.
        session.Status       = ParkingSessionStatus.ExitProcessing;
        session.ExitImageUrl = request.ExitImageUrl;

        await _sessionRepo.UpdateAsync(session, ct);
        await WriteLogAsync(request, request.SessionId, authenticatedActorId, EventProcessingStatus.Accepted, null, ct);

        return new IngestEventResponse
        {
            Success          = true,
            ProcessingStatus = EventProcessingStatus.Accepted,
            EventId          = request.EventId,
            SessionId        = request.SessionId,
            SessionStatus    = ParkingSessionStatus.ExitProcessing,
            Message          = $"Đã nhận tín hiệu cổng ra cho phiên '{request.SessionId}'. Phiên chưa đóng và slot chưa được giải phóng."
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────────────────
    private async Task<IngestEventResponse> RejectAndLogAsync(
        IngestEventRequest request, string? authenticatedActorId, string reason, CancellationToken ct)
    {
        await WriteLogAsync(request, request.SessionId, authenticatedActorId, EventProcessingStatus.Rejected, reason, ct);
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
        string?            authenticatedActorId,
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
            TriggeredBy       = authenticatedActorId,
            PayloadVersion    = request.PayloadVersion,
            ProcessingStatus  = status,
            RejectReason      = rejectReason,
            EventTimestampUtc = request.EventTimestampUtc ?? DateTimeOffset.UtcNow,
            ReceivedAtUtc     = DateTimeOffset.UtcNow
        }, ct);
    }

    private static string? NormalizePlate(string? plate) =>
        string.IsNullOrWhiteSpace(plate) ? null : plate.Trim().ToUpperInvariant();
}
