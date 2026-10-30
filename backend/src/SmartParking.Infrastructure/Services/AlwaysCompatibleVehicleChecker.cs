using SmartParking.Application.Common.Interfaces;

namespace SmartParking.Infrastructure.Services;

/// <summary>
/// Stub triển khai tạm thời – luôn trả về compatible (null warning).
/// TODO: Thay bằng EF Core query ParkingSlot.VehicleType khi Phụng's branch được merge.
/// </summary>
public class AlwaysCompatibleVehicleChecker : IVehicleCompatibilityChecker
{
    public Task<string?> CheckAsync(string slotCode, string? fuelType, CancellationToken ct = default)
        => Task.FromResult<string?>(null);
}
