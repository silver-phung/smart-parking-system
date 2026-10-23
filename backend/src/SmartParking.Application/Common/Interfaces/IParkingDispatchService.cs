using SmartParking.Application.Common.Models;
using SmartParking.Application.DTOs.Dispatches;

namespace SmartParking.Application.Common.Interfaces;

public interface IParkingDispatchService
{
    Task<(bool Success, string Message, ParkingDispatchDto? Data)> CreateDispatchAsync(
        CreateDispatchRequestDto request,
        long? actorId,
        string actorRole,
        string? ipAddress,
        CancellationToken ct = default);

    Task<PagedResult<ParkingDispatchDto>> GetDispatchesAsync(
        ParkingDispatchFilterDto filter,
        long? currentUserId,
        bool isAdmin,
        CancellationToken ct = default);

    Task<ParkingDispatchDto?> GetDispatchByIdAsync(
        long id,
        long? currentUserId,
        bool isAdmin,
        CancellationToken ct = default);

    Task<ParkingDispatchDto?> GetActiveDispatchBySessionIdAsync(
        long sessionId,
        long? currentUserId,
        bool isAdmin,
        CancellationToken ct = default);

    Task<(bool Success, string Message, ParkingDispatchDto? Data)> ReassignSlotAsync(
        long dispatchId,
        AssignSlotRequestDto request,
        long? actorId,
        string actorRole,
        string? ipAddress,
        CancellationToken ct = default);

    Task<(bool Success, string Message, ParkingDispatchDto? Data)> ConfirmOccupiedAsync(
        long dispatchId,
        ConfirmOccupiedRequestDto request,
        long? actorId,
        string actorRole,
        string? ipAddress,
        CancellationToken ct = default);

    Task<(bool Success, string Message, ParkingDispatchDto? Data)> ReportWrongSlotAsync(
        long dispatchId,
        ReportWrongSlotRequestDto request,
        long? actorId,
        string actorRole,
        string? ipAddress,
        CancellationToken ct = default);

    Task<(bool Success, string Message)> CancelDispatchAsync(
        long dispatchId,
        CancelDispatchRequestDto request,
        long? actorId,
        string actorRole,
        string? ipAddress,
        CancellationToken ct = default);
}
