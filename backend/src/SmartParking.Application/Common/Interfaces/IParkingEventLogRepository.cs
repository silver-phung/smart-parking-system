using SmartParking.Domain.Entities;

namespace SmartParking.Application.Common.Interfaces;

/// <summary>Repository thao tác với bảng parking_event_logs. Append-only.</summary>
public interface IParkingEventLogRepository
{
    /// <summary>
    /// Tìm bản ghi log theo EventId (Idempotency Check).
    /// Trả về null nếu chưa tồn tại.
    /// </summary>
    Task<ParkingEventLog?> FindByEventIdAsync(string eventId, CancellationToken ct = default);

    /// <summary>Ghi một sự kiện mới. Chỉ gọi sau khi FindByEventId trả về null.</summary>
    Task AddAsync(ParkingEventLog log, CancellationToken ct = default);

    /// <summary>
    /// Lấy log gần đây nhất theo bãi đỗ – phục vụ màn hình giám sát real-time.
    /// </summary>
    Task<IReadOnlyList<ParkingEventLog>> GetRecentByLotAsync(
        int parkingLotId, int limit = 100, CancellationToken ct = default);
}
