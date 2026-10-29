using SmartParking.Application.Common.Models;
using SmartParking.Application.DTOs.Sessions;

namespace SmartParking.Application.Common.Interfaces;

public interface IParkingSessionService
{
    Task<(bool Success, string Message, ParkingSessionDto? Data)> CheckInAsync(CheckInRequestDto request, long? actorId, string actorRole, string? ipAddress, CancellationToken ct = default);
    Task<(bool Success, string Message, ParkingSessionDto? Data)> CheckOutAsync(long sessionId, CheckOutRequestDto request, long? actorId, string actorRole, string? ipAddress, CancellationToken ct = default);
    Task<PagedResult<ParkingSessionDto>> GetActiveSessionsAsync(ParkingSessionFilterDto filter, long? currentUserId, string currentUserRole, CancellationToken ct = default);
    Task<PagedResult<ParkingSessionDto>> GetUserSessionsAsync(long userId, ParkingSessionFilterDto filter, CancellationToken ct = default);
    Task<ParkingSessionDto?> GetSessionByIdAsync(long id, long? currentUserId, string currentUserRole, CancellationToken ct = default);
    Task<ParkingSessionDto?> GetActiveSessionByPlateAsync(string plateNumber, long? currentUserId, string currentUserRole, CancellationToken ct = default);
    Task<IReadOnlyList<UnmatchedSessionDto>> GetUnmatchedSessionsAsync(int lotId, long? actorId, string actorRole, CancellationToken ct = default);
    Task<IReadOnlyList<UnmatchedSlotDto>> GetUnmatchedOccupiedSlotsAsync(int lotId, long? actorId, string actorRole, CancellationToken ct = default);
    Task<(bool Success, string Message, ParkingSessionDto? Data)> VerifySessionSlotAsync(long sessionId, VerifySessionSlotRequestDto request, long? actorId, string actorRole, string? ipAddress, CancellationToken ct = default);
    Task<(bool Success, string Message, ParkingSessionDto? Data)> ReportSessionWrongSlotAsync(long sessionId, ReportWrongSlotSessionRequestDto request, long? actorId, string actorRole, string? ipAddress, CancellationToken ct = default);
    Task<(bool Success, string Message, ParkingSessionDto? Data)> RecordPaymentClearanceAsync(long sessionId, decimal amountPaid, string transactionRef, string? paymentMethod, long? actorId, string actorRole, string? ipAddress, CancellationToken ct = default);
}
