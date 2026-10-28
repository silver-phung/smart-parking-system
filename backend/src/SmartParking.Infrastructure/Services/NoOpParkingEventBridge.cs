using Microsoft.Extensions.Logging;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Events.Dtos;

namespace SmartParking.Infrastructure.Services;

/// <summary>
/// No-op bridge – không làm gì, chỉ ghi log.
/// Dùng trong Sprint 3 khi Phụng's DB module chưa được merge vào nhánh này.
///
/// TODO (sau merge feature/phung-database → develop):
///   Thay bằng <see cref="LiveParkingEventBridge"/> trong DependencyInjection.cs
/// </summary>
public class NoOpParkingEventBridge : IParkingEventBridge
{
    private readonly ILogger<NoOpParkingEventBridge> _logger;

    public NoOpParkingEventBridge(ILogger<NoOpParkingEventBridge> logger)
    {
        _logger = logger;
    }

    public Task OnVehicleEnteredAsync(IngestEventRequest request, string sessionId, CancellationToken ct = default)
    {
        _logger.LogDebug(
            "[Bridge NoOp] VehicleEntered – plate={Plate} session={Session} (chưa sync sang DB Phụng)",
            request.CarPlate, sessionId);
        return Task.CompletedTask;
    }

    public Task OnSlotParkedAsync(IngestEventRequest request, string sessionId, CancellationToken ct = default)
    {
        _logger.LogDebug(
            "[Bridge NoOp] SlotParked – slot={Slot} session={Session} (chưa sync sang DB Phụng)",
            request.SlotCode, sessionId);
        return Task.CompletedTask;
    }

    public Task OnVehicleExitedAsync(IngestEventRequest request, string sessionId, CancellationToken ct = default)
    {
        _logger.LogDebug(
            "[Bridge NoOp] VehicleExited – plate={Plate} session={Session} (chưa sync sang DB Phụng)",
            request.CarPlate, sessionId);
        return Task.CompletedTask;
    }
}
