using SmartParking.Domain.Entities;

namespace SmartParking.Application.Common.Interfaces;

/// <summary>Repository thao tác với bảng parking_sessions.</summary>
public interface IParkingSessionRepository
{
    /// <summary>Tìm phiên theo SessionId (UUID).</summary>
    Task<EventParkingSession?> FindBySessionIdAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Tìm phiên đang mở theo biển số đã được Staff xác nhận.
    /// OCR tạm ở cổng không được dùng để chặn phiên khác.
    /// </summary>
    Task<EventParkingSession?> FindActiveByCarPlateAsync(string carPlate, CancellationToken ct = default);

    /// <summary>Tìm phiên đã xác nhận đang chiếm actual slot.</summary>
    Task<EventParkingSession?> FindActiveBySlotAsync(
        int parkingLotId, string slotCode, CancellationToken ct = default);

    /// <summary>Tạo phiên mới (khi xe qua cổng vào).</summary>
    Task AddAsync(EventParkingSession session, CancellationToken ct = default);

    /// <summary>Cập nhật phiên (gán slot, đóng phiên, ghi cảnh báo, v.v.).</summary>
    Task UpdateAsync(EventParkingSession session, CancellationToken ct = default);

    /// <summary>
    /// Lấy danh sách phiên đang mở tại một bãi.
    /// Dùng cho màn hình Staff điều phối xem xe đang chờ gán slot.
    /// </summary>
    Task<IReadOnlyList<EventParkingSession>> GetActiveByLotAsync(
        int parkingLotId, CancellationToken ct = default);
}
