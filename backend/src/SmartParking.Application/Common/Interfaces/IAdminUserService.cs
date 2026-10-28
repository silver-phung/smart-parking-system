using SmartParking.Application.Common.Models;
using SmartParking.Application.DTOs.AdminUsers;

namespace SmartParking.Application.Common.Interfaces;

public interface IAdminUserService
{
    Task<PagedResult<AdminUserSummaryDto>> GetUsersAsync(AdminUserFilterDto filter, CancellationToken ct = default);
    Task<AdminUserDetailDto?> GetUserByIdAsync(long id, CancellationToken ct = default);
    Task<(bool Success, int StatusCode, string Message, AdminUserDetailDto? Data)> CreateUserAsync(
        AdminCreateUserRequestDto request,
        long actorId,
        string? ipAddress,
        CancellationToken ct = default);
    Task<(bool Success, int StatusCode, string Message, AdminUserDetailDto? Data)> UpdateUserStatusAsync(
        long id,
        AdminUpdateStatusRequestDto request,
        long actorId,
        string? ipAddress,
        CancellationToken ct = default);
    Task<(bool Success, int StatusCode, string Message, AdminUserDetailDto? Data)> UpdateUserRoleAsync(
        long id,
        AdminUpdateRoleRequestDto request,
        long actorId,
        string? ipAddress,
        CancellationToken ct = default);
}
