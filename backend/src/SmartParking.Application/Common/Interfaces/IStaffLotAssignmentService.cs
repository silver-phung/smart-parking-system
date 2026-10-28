using SmartParking.Application.Common.Models;
using SmartParking.Application.DTOs.StaffAssignments;

namespace SmartParking.Application.Common.Interfaces;

public interface IStaffLotAssignmentService
{
    Task<(bool Success, string Message, StaffLotAssignmentDto? Data)> CreateAssignmentAsync(
        CreateStaffAssignmentRequestDto request,
        long actorId,
        string? ipAddress,
        CancellationToken ct = default);

    Task<PagedResult<StaffLotAssignmentDto>> GetAssignmentsAsync(
        StaffAssignmentFilterDto filter,
        long actorId,
        string actorRole,
        CancellationToken ct = default);

    Task<StaffLotAssignmentDto?> GetAssignmentByIdAsync(
        long id,
        long actorId,
        string actorRole,
        CancellationToken ct = default);

    Task<List<AssignedLotSummaryDto>> GetMyAssignedLotsAsync(
        long userId,
        CancellationToken ct = default);

    Task<(bool Success, string Message)> RevokeAssignmentAsync(
        long id,
        RevokeStaffAssignmentRequestDto request,
        long actorId,
        string? ipAddress,
        CancellationToken ct = default);
}
