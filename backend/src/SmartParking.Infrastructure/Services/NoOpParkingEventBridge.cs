using Microsoft.Extensions.Logging;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Events.Dtos;

namespace SmartParking.Infrastructure.Services;

/// <summary>
/// No-op an toàn: không giả vờ đã đồng bộ sang DB core.
/// </summary>
public class NoOpParkingEventBridge : IParkingEventBridge
{
    private readonly ILogger<NoOpParkingEventBridge> _logger;

    public NoOpParkingEventBridge(ILogger<NoOpParkingEventBridge> logger)
    {
        _logger = logger;
    }

    public Task OnParkingVerifiedAsync(
        IngestEventRequest request,
        string sessionId,
        string authenticatedActorId,
        CancellationToken ct = default)
    {
        _logger.LogWarning(
            "Parking verification chưa được đồng bộ sang core: session={Session} slot={Slot} actor={Actor}",
            sessionId, request.SlotCode, authenticatedActorId);
        return Task.CompletedTask;
    }
}
