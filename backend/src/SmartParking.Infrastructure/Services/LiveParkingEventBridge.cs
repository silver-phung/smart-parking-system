namespace SmartParking.Infrastructure.Services;

#if LIVE_BRIDGE
using Microsoft.Extensions.Logging;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Events.Dtos;

/// <summary>
/// Adapter dành cho core dispatch service sau khi hai module dùng chung hợp đồng.
/// Không gọi CheckIn/CheckOut trực tiếp: OCR entry chưa là biển số chính thức và
/// exit signal chưa chứng minh đủ phí hoặc xe đã qua cổng.
/// </summary>
public sealed class LiveParkingEventBridge : IParkingEventBridge
{
    private readonly ILogger<LiveParkingEventBridge> _logger;

    public LiveParkingEventBridge(ILogger<LiveParkingEventBridge> logger)
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
            "LIVE_BRIDGE cần nối ParkingDispatchService xác nhận actual slot; " +
            "không tự đổi core session. Session={Session} Slot={Slot} Actor={Actor}",
            sessionId, request.SlotCode, authenticatedActorId);
        return Task.CompletedTask;
    }
}
#else
/// <summary>Placeholder khi LIVE_BRIDGE chưa được định nghĩa.</summary>
public sealed class LiveParkingEventBridgePlaceholder
{
}
#endif
