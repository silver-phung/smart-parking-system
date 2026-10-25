namespace SmartParking.Application.Common.Interfaces;

/// <summary>
/// Interface kiểm tra tương thích loại xe với ô đậu.
/// TODO: Khi Phụng's EF Core branch được merge, triển khai thực tế bằng cách query ParkingSlot.VehicleType.
/// </summary>
public interface IVehicleCompatibilityChecker
{
    /// <summary>
    /// Kiểm tra xem FuelType của xe có phù hợp với loại ô đậu không.
    /// </summary>
    /// <param name="slotCode">Mã ô đậu, ví dụ "A1-012".</param>
    /// <param name="fuelType">Loại nhiên liệu xe: "gasoline" | "electric" | "unknown".</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>null nếu tương thích; warning code string nếu không tương thích.</returns>
    Task<string?> CheckAsync(string slotCode, string? fuelType, CancellationToken ct = default);
}
