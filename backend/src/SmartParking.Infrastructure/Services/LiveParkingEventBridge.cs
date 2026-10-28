namespace SmartParking.Infrastructure.Services;

#if LIVE_BRIDGE
using Microsoft.Extensions.Logging;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.DTOs.Sessions;
using SmartParking.Application.Events.Dtos;

/// <summary>
public class LiveParkingEventBridge : IParkingEventBridge
{
    private readonly IParkingSessionService          _sessionService;
    private readonly ILogger<LiveParkingEventBridge> _logger;

    public LiveParkingEventBridge(
        IParkingSessionService          sessionService,
        ILogger<LiveParkingEventBridge> logger)
    {
        _sessionService = sessionService;
        _logger         = logger;
    }

    public async Task OnVehicleEnteredAsync(
        IngestEventRequest request, string sessionId, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[Bridge Live] VehicleEntered – tạo CheckIn trong DB Phụng: plate={Plate} lot={Lot}",
            request.CarPlate, request.ParkingLotId);

        var checkInRequest = new CheckInRequestDto
        {
            LotId           = request.ParkingLotId,
            PlateNumber     = request.CarPlate ?? string.Empty,
            CheckInImageUrl = request.EntryImageUrl
        };

        // actorId = null (system), actorRole = Staff (cổng vào)
        var (success, message, session) = await _sessionService.CheckInAsync(
            checkInRequest, actorId: null, actorRole: "Staff", ipAddress: null, ct);

        if (!success)
        {
            _logger.LogWarning(
                "[Bridge Live] CheckIn thất bại: {Message} – plate={Plate}",
                message, request.CarPlate);
        }
        else
        {
            _logger.LogInformation(
                "[Bridge Live] CheckIn thành công – PhungSessionId={PId} plate={Plate}",
                session?.Id, request.CarPlate);
        }
    }

    public Task OnSlotParkedAsync(
        IngestEventRequest request, string sessionId, CancellationToken ct = default)
    {
        // Slot assignment sync là optional – Dispatch module của Phụng xử lý riêng.
        _logger.LogDebug(
            "[Bridge Live] SlotParked – slot={Slot} session={Session} (Dispatch do Phụng quản lý)",
            request.SlotCode, sessionId);
        return Task.CompletedTask;
    }

    public async Task OnVehicleExitedAsync(
        IngestEventRequest request, string sessionId, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[Bridge Live] VehicleExited – cần lookup PhungSessionId từ plate={Plate}",
            request.CarPlate);

        // Tra cứu active session của Phụng theo biển số để lấy Id
        var phungSession = await _sessionService.GetActiveSessionByPlateAsync(
            request.CarPlate ?? string.Empty,
            currentUserId: null, currentUserRole: "Admin", ct);

        if (phungSession is null)
        {
            _logger.LogWarning(
                "[Bridge Live] Không tìm thấy Phụng's session cho plate={Plate} – bỏ qua checkout.",
                request.CarPlate);
            return;
        }

        var checkOutRequest = new CheckOutRequestDto
        {
            CheckOutImageUrl = request.ExitImageUrl
        };

        var (success, message, _) = await _sessionService.CheckOutAsync(
            phungSession.Id, checkOutRequest,
            actorId: null, actorRole: "Staff", ipAddress: null, ct);

        if (!success)
        {
            _logger.LogWarning(
                "[Bridge Live] CheckOut thất bại: {Message} – PhungSessionId={PId}",
                message, phungSession.Id);
        }
    }
}
#else
/// <summary>
/// Placeholder khi LIVE_BRIDGE chưa được định nghĩa.
/// File này tồn tại để giữ reference và comment đầy đủ.
/// </summary>
public sealed class LiveParkingEventBridgePlaceholder
{
    // Xem LiveParkingEventBridge bên trên (compile với #define LIVE_BRIDGE)
}
#endif
