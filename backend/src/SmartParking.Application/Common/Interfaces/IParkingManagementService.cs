using SmartParking.Application.Common.Models;
using SmartParking.Application.DTOs.Parking;

namespace SmartParking.Application.Common.Interfaces;

public interface IParkingManagementService
{
    // Parking Lots
    Task<PagedResult<ParkingLotDto>> GetLotsAsync(ParkingLotFilterDto filter, bool isAdminOrStaff, CancellationToken ct = default);
    Task<ParkingLotDetailDto?> GetLotByIdAsync(int id, bool isAdminOrStaff, CancellationToken ct = default);
    Task<(bool Success, string Message, ParkingLotDto? Lot)> CreateLotAsync(CreateParkingLotRequestDto request, long? actorId, CancellationToken ct = default);
    Task<(bool Success, string Message, ParkingLotDto? Lot)> UpdateLotAsync(int id, UpdateParkingLotRequestDto request, long? actorId, CancellationToken ct = default);
    Task<(bool Success, string Message, ParkingLotDto? Lot)> UpdateLotStatusAsync(int id, UpdateLotStatusRequestDto request, long? actorId, CancellationToken ct = default);
    Task<(bool Success, string Message)> DeleteLotAsync(int id, long? actorId, CancellationToken ct = default);
    Task<(bool Success, string Message, ProvisionLotLayoutResultDto? Result)> ProvisionStandardLayoutAsync(int lotId, long? actorId, CancellationToken ct = default);

    // Parking Floors
    Task<IReadOnlyList<ParkingFloorDto>> GetFloorsByLotIdAsync(int lotId, bool? isActiveOnly, CancellationToken ct = default);
    Task<ParkingFloorDto?> GetFloorByIdAsync(int id, CancellationToken ct = default);
    Task<(bool Success, string Message, ParkingFloorDto? Floor)> CreateFloorAsync(int lotId, CreateParkingFloorRequestDto request, long? actorId, CancellationToken ct = default);
    Task<(bool Success, string Message, ParkingFloorDto? Floor)> UpdateFloorAsync(int id, UpdateParkingFloorRequestDto request, long? actorId, CancellationToken ct = default);
    Task<(bool Success, string Message)> DeleteFloorAsync(int id, long? actorId, CancellationToken ct = default);

    // Parking Slots
    Task<PagedResult<ParkingSlotDto>> GetSlotsByFloorIdAsync(int floorId, ParkingSlotFilterDto filter, CancellationToken ct = default);
    Task<ParkingSlotDetailDto?> GetSlotByIdAsync(long id, bool includeInternalDetails, CancellationToken ct = default);
    Task<(bool Success, string Message, ParkingSlotDto? Slot)> CreateSlotAsync(int floorId, CreateParkingSlotRequestDto request, long? actorId, CancellationToken ct = default);
    Task<(bool Success, string Message, ParkingSlotDto? Slot)> UpdateSlotAsync(long id, UpdateParkingSlotRequestDto request, long? actorId, CancellationToken ct = default);
    Task<(bool Success, string Message, ParkingSlotDto? Slot)> UpdateSlotPhysicalStatusAsync(long id, UpdateSlotPhysicalStatusRequestDto request, long? actorId, string actorRole, string? ipAddress, CancellationToken ct = default);
    Task<(bool Success, string Message)> DeleteSlotAsync(long id, long? actorId, CancellationToken ct = default);
}
