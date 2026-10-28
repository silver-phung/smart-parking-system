using SmartParking.Application.Common.Models;
using SmartParking.Application.DTOs.Vehicles;

namespace SmartParking.Application.Common.Interfaces;

public interface IVehicleService
{
    Task<PagedResult<VehicleDto>> GetVehiclesAsync(VehicleFilterDto filter, long? currentUserId, bool isAdmin, CancellationToken ct = default);
    Task<VehicleDto?> GetVehicleByIdAsync(long id, long? currentUserId, bool isAdmin, CancellationToken ct = default);
    Task<VehicleDto?> GetVehicleByPlateAsync(string plateNumber, long? currentUserId, bool isAdmin, CancellationToken ct = default);
    Task<(bool Success, string Message, VehicleDto? Data)> CreateVehicleAsync(CreateVehicleRequestDto request, long? currentUserId, bool isAdmin, CancellationToken ct = default);
    Task<(bool Success, string Message, VehicleDto? Data)> UpdateVehicleAsync(long id, UpdateVehicleRequestDto request, long? currentUserId, bool isAdmin, CancellationToken ct = default);
    Task<(bool Success, string Message)> DeleteVehicleAsync(long id, long? currentUserId, bool isAdmin, CancellationToken ct = default);
}
