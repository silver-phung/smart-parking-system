using SmartParking.Application.Common.Models;
using SmartParking.Application.DTOs.Sessions;

namespace SmartParking.Application.Common.Interfaces;

public interface IParkingSessionService
{
    Task<(bool Success, string Message, ParkingSessionDto? Data)> CheckInAsync(CheckInRequestDto request, long? actorId, string actorRole, string? ipAddress, CancellationToken ct = default);
    Task<(bool Success, string Message, ParkingSessionDto? Data)> CheckOutAsync(long sessionId, CheckOutRequestDto request, long? actorId, string actorRole, string? ipAddress, CancellationToken ct = default);
    Task<PagedResult<ParkingSessionDto>> GetActiveSessionsAsync(ParkingSessionFilterDto filter, long? currentUserId, bool isAdmin, CancellationToken ct = default);
    Task<PagedResult<ParkingSessionDto>> GetUserSessionsAsync(long userId, ParkingSessionFilterDto filter, CancellationToken ct = default);
    Task<ParkingSessionDto?> GetSessionByIdAsync(long id, long? currentUserId, bool isAdmin, CancellationToken ct = default);
    Task<ParkingSessionDto?> GetActiveSessionByPlateAsync(string plateNumber, long? currentUserId, bool isAdmin, CancellationToken ct = default);
}
