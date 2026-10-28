using SmartParking.Application.Events.Dtos;

namespace SmartParking.Application.Common.Interfaces;

/// <summary>
/// Bridge interface kết nối Event Ingestion Pipeline (Thức) với
/// Session/Dispatch Management (Phụng).
///
/// Mục đích:
///   Khi ParkingEventService xử lý VehicleEntered / SlotParked / VehicleExited,
///   nó gọi bridge để đồng bộ trạng thái sang module của Phụng (DB chính thức).
///
/// Hiện tại dùng <see cref="NoOpParkingEventBridge"/> (không làm gì).
/// Sau khi Phụng's branch merge: triển khai <see cref="LiveParkingEventBridge"/>
///   gọi IParkingSessionService.CheckInAsync / CheckOutAsync.
/// </summary>
public interface IParkingEventBridge
{
    /// <summary>
    /// Được gọi sau khi VehicleEntered được accepted.
    /// Bridge tạo ParkingSession trong DB chính của Phụng.
    /// </summary>
    Task OnVehicleEnteredAsync(IngestEventRequest request, string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Được gọi sau khi SlotParked được accepted.
    /// Bridge cập nhật CurrentSlotId trong ParkingSession của Phụng.
    /// </summary>
    Task OnSlotParkedAsync(IngestEventRequest request, string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Được gọi sau khi VehicleExited được accepted.
    /// Bridge gọi CheckOutAsync trên module của Phụng (tính phí, giải phóng slot).
    /// </summary>
    Task OnVehicleExitedAsync(IngestEventRequest request, string sessionId, CancellationToken ct = default);
}
