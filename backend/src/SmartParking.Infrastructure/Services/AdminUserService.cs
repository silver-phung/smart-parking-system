using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Common.Models;
using SmartParking.Application.DTOs.AdminUsers;
using SmartParking.Application.DTOs.StaffAssignments;
using SmartParking.Domain.Constants;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Services;

public class AdminUserService : IAdminUserService
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly ILogger<AdminUserService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    private static readonly SemaphoreSlim AdminMutationLock = new(1, 1);

    public AdminUserService(
        IApplicationDbContext context,
        IPasswordHasherService passwordHasher,
        ILogger<AdminUserService> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<PagedResult<AdminUserSummaryDto>> GetUsersAsync(AdminUserFilterDto filter, CancellationToken ct = default)
    {
        var query = _context.Users
            .Include(u => u.Role)
            .AsNoTracking()
            .AsQueryable();

        // 1. Search by username, email or full name
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLowerInvariant();
            query = query.Where(u =>
                u.Username.ToLower().Contains(term) ||
                u.Email.ToLower().Contains(term) ||
                u.FullName.ToLower().Contains(term));
        }

        // 2. Filter by role
        if (!string.IsNullOrWhiteSpace(filter.Role))
        {
            var roleCode = filter.Role.Trim().ToUpperInvariant();
            query = query.Where(u => u.Role.Code == roleCode);
        }

        // 3. Filter by active status
        if (filter.IsActive.HasValue)
        {
            query = query.Where(u => u.IsActive == filter.IsActive.Value);
        }

        // 4. Filter by approval status
        if (!string.IsNullOrWhiteSpace(filter.ApprovalStatus))
        {
            var status = filter.ApprovalStatus.Trim().ToUpperInvariant();
            query = query.Where(u => u.ApprovalStatus == status);
        }

        var totalCount = await query.CountAsync(ct);

        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 10 : (filter.PageSize > 100 ? 100 : filter.PageSize);

        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .ThenBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AdminUserSummaryDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                FullName = u.FullName,
                PhoneNumber = u.PhoneNumber,
                Role = u.Role.Code,
                IsActive = u.IsActive,
                IsEmailVerified = u.IsEmailVerified,
                ApprovalStatus = u.ApprovalStatus,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt
            })
            .ToListAsync(ct);

        return PagedResult<AdminUserSummaryDto>.Create(items, totalCount, page, pageSize);
    }

    public async Task<AdminUserDetailDto?> GetUserByIdAsync(long id, CancellationToken ct = default)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.ApprovedByUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user == null)
        {
            return null;
        }

        var activeAssignments = await _context.StaffLotAssignments
            .Include(a => a.Lot)
            .AsNoTracking()
            .Where(a => a.UserId == user.Id && a.IsActive)
            .Select(a => new AssignedLotSummaryDto
            {
                LotId = a.LotId,
                LotName = a.Lot.Name,
                LotAddress = a.Lot.Address,
                IsActive = a.IsActive,
                AssignedAt = a.AssignedAt
            })
            .ToListAsync(ct);

        var vehiclesCount = await _context.Vehicles
            .AsNoTracking()
            .CountAsync(v => v.UserId == user.Id, ct);

        return MapToDetailDto(user, activeAssignments, vehiclesCount);
    }

    public async Task<(bool Success, int StatusCode, string Message, AdminUserDetailDto? Data)> CreateUserAsync(
        AdminCreateUserRequestDto request,
        long actorId,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var targetRoleCode = request.Role.Trim().ToUpperInvariant();
        if (targetRoleCode != Roles.Staff && targetRoleCode != Roles.Manager)
        {
            return (false, 400, $"Chỉ cho phép quản trị viên tạo tài khoản nội bộ với vai trò '{Roles.Staff}' hoặc '{Roles.Manager}'. Không hỗ trợ tạo tài khoản '{request.Role}' qua giao diện này.", null);
        }

        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Code == targetRoleCode, ct);
        if (role == null)
        {
            return (false, 400, $"Không tìm thấy vai trò '{targetRoleCode}' trong cơ sở dữ liệu.", null);
        }

        var normalizedUsername = request.Username.Trim().ToLowerInvariant();
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var conflict = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username.ToLower() == normalizedUsername || u.Email.ToLower() == normalizedEmail, ct);

        if (conflict != null)
        {
            if (string.Equals(conflict.Username, normalizedUsername, StringComparison.OrdinalIgnoreCase))
            {
                return (false, 409, "Tên đăng nhập đã được sử dụng.", null);
            }
            return (false, 409, "Địa chỉ email đã được sử dụng.", null);
        }

        var actor = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == actorId, ct);
        var actorUsername = actor?.Username ?? "admin";

        var user = new User
        {
            Username = normalizedUsername,
            Email = normalizedEmail,
            RoleId = role.Id,
            Role = role,
            FullName = request.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            IsActive = request.IsActive,
            IsEmailVerified = true,
            ApprovalStatus = ApprovalStatuses.Approved,
            ApprovedBy = actorId,
            ApprovedAt = DateTime.UtcNow,
            FailedLoginAttempts = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        await using var transaction = await _context.BeginTransactionAsync(ct);
        try
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync(ct);

            var audit = new AuditLog
            {
                Action = "CREATE_USER",
                TargetEntity = "users",
                TargetId = user.Id.ToString(),
                NewValues = JsonSerializer.Serialize(new
                {
                    user.Id,
                    user.Username,
                    user.Email,
                    Role = role.Code,
                    user.IsActive,
                    user.ApprovalStatus,
                    CreatedBy = actorUsername,
                    IpAddress = ipAddress
                }, JsonOptions),
                ActorId = actorId,
                IpAddress = ipAddress,
                CreatedAt = DateTime.UtcNow
            };
            _context.AuditLogs.Add(audit);
            await _context.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);

            _logger.LogInformation("Admin ID {AdminId} created user {UserId} ({Username}) with role {Role}", actorId, user.Id, user.Username, role.Code);

            var detail = MapToDetailDto(user, [], 0, actorUsername);
            return (true, 201, "Tạo tài khoản người dùng thành công.", detail);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(ct);

            if (IsPostgresUniqueViolation(ex, "uq_users_username"))
            {
                _logger.LogWarning(ex, "Unique constraint violation uq_users_username during user creation for username {Username}", normalizedUsername);
                return (false, 409, "Tên đăng nhập đã được sử dụng.", null);
            }

            if (IsPostgresUniqueViolation(ex, "uq_users_email"))
            {
                _logger.LogWarning(ex, "Unique constraint violation uq_users_email during user creation for email {Email}", normalizedEmail);
                return (false, 409, "Địa chỉ email đã được sử dụng.", null);
            }

            if (IsPostgresUniqueViolation(ex, string.Empty))
            {
                _logger.LogWarning(ex, "Unique constraint violation 23505 during user creation for username {Username} hoặc email {Email}", normalizedUsername, normalizedEmail);
                return (false, 409, "Tên đăng nhập hoặc Email đã tồn tại trong hệ thống.", null);
            }

            _logger.LogError(ex, "Lỗi cơ sở dữ liệu khi tạo người dùng {Username} bởi Admin ID {AdminId}", request.Username, actorId);
            throw;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Lỗi khi tạo người dùng {Username} bởi Admin ID {AdminId}", request.Username, actorId);
            throw;
        }
    }

    public async Task<(bool Success, int StatusCode, string Message, AdminUserDetailDto? Data)> UpdateUserStatusAsync(
        long id,
        AdminUpdateStatusRequestDto request,
        long actorId,
        string? ipAddress,
        CancellationToken ct = default)
    {
        await AdminMutationLock.WaitAsync(ct);
        try
        {
            await using var transaction = await _context.BeginTransactionAsync(ct);
            try
            {
                if (_context.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
                {
                    await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(990099);", ct);
                }

                var targetUser = await _context.Users
                    .Include(u => u.Role)
                    .Include(u => u.ApprovedByUser)
                    .FirstOrDefaultAsync(u => u.Id == id, ct);

                if (targetUser == null)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, 404, $"Không tìm thấy người dùng với ID: {id}.", null);
                }

                // Prevent admin from disabling themselves
                if (targetUser.Id == actorId && !request.IsActive)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, 400, "Quản trị viên không thể tự vô hiệu hóa tài khoản của chính mình.", null);
                }

                // Prevent disabling the last active admin
                if (targetUser.Role.Code == Roles.Admin && !request.IsActive)
                {
                    if (await IsLastActiveAdminAsync(targetUser.Id, ct))
                    {
                        await transaction.RollbackAsync(ct);
                        return (false, 400, "Không thể vô hiệu hóa quản trị viên (ADMIN) đang hoạt động cuối cùng của hệ thống.", null);
                    }
                }

                if (targetUser.IsActive == request.IsActive)
                {
                    await transaction.RollbackAsync(ct);
                    var currentDetail = await GetUserByIdAsync(id, ct);
                    return (true, 200, $"Trạng thái tài khoản '{targetUser.Username}' đã là {(request.IsActive ? "kích hoạt" : "vô hiệu hóa")}.", currentDetail);
                }

                var oldStatus = targetUser.IsActive;
                targetUser.IsActive = request.IsActive;
                targetUser.UpdatedAt = DateTime.UtcNow;

                int revokedAssignmentsCount = 0;
                if (!request.IsActive)
                {
                    // Soft-revoke active staff lot assignments when account is disabled
                    var activeAssignments = await _context.StaffLotAssignments
                        .Include(a => a.Lot)
                        .Where(a => a.UserId == targetUser.Id && a.IsActive)
                        .ToListAsync(ct);

                    var reason = string.IsNullOrWhiteSpace(request.Reason)
                        ? "Tự động thu hồi do tài khoản nhân sự bị vô hiệu hóa."
                        : $"Tự động thu hồi do tài khoản bị vô hiệu hóa: {request.Reason.Trim()}";

                    foreach (var a in activeAssignments)
                    {
                        a.IsActive = false;
                        a.RevokedBy = actorId;
                        a.RevokedAt = DateTime.UtcNow;
                        a.RevocationReason = reason;
                        a.UpdatedAt = DateTime.UtcNow;
                        revokedAssignmentsCount++;

                        _context.AuditLogs.Add(new AuditLog
                        {
                            Action = "REVOKE_STAFF_LOT",
                            TargetEntity = "staff_lot_assignments",
                            TargetId = a.Id.ToString(),
                            OldValues = JsonSerializer.Serialize(new { a.Id, a.LotId, Status = "ACTIVE" }, JsonOptions),
                            NewValues = JsonSerializer.Serialize(new { Status = "REVOKED", RevocationReason = reason, RevokedBy = actorId }, JsonOptions),
                            ActorId = actorId,
                            IpAddress = ipAddress,
                            CreatedAt = DateTime.UtcNow
                        });
                    }

                    // Revoke all active refresh tokens so user cannot obtain new access tokens
                    var activeTokens = await _context.RefreshTokens
                        .Where(rt => rt.UserId == targetUser.Id && !rt.IsRevoked)
                        .ToListAsync(ct);

                    foreach (var rt in activeTokens)
                    {
                        rt.IsRevoked = true;
                        rt.RevokedAt = DateTime.UtcNow;
                    }
                }

                var audit = new AuditLog
                {
                    Action = "UPDATE_USER_STATUS",
                    TargetEntity = "users",
                    TargetId = targetUser.Id.ToString(),
                    OldValues = JsonSerializer.Serialize(new { IsActive = oldStatus }, JsonOptions),
                    NewValues = JsonSerializer.Serialize(new
                    {
                        IsActive = targetUser.IsActive,
                        request.Reason,
                        RevokedAssignmentsCount = revokedAssignmentsCount,
                        IpAddress = ipAddress
                    }, JsonOptions),
                    ActorId = actorId,
                    IpAddress = ipAddress,
                    CreatedAt = DateTime.UtcNow
                };
                _context.AuditLogs.Add(audit);

                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                _logger.LogInformation("Admin ID {AdminId} updated status of User ID {UserId} ({Username}) to {IsActive}", actorId, targetUser.Id, targetUser.Username, targetUser.IsActive);

                var updatedDetail = await GetUserByIdAsync(id, ct);
                var statusText = targetUser.IsActive ? "kích hoạt" : "vô hiệu hóa";
                return (true, 200, $"Cập nhật trạng thái tài khoản '{targetUser.Username}' thành {statusText} thành công.", updatedDetail);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Lỗi khi cập nhật trạng thái người dùng ID {UserId}", id);
                throw;
            }
        }
        finally
        {
            AdminMutationLock.Release();
        }
    }

    public async Task<(bool Success, int StatusCode, string Message, AdminUserDetailDto? Data)> UpdateUserRoleAsync(
        long id,
        AdminUpdateRoleRequestDto request,
        long actorId,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var targetRoleCode = request.Role.Trim().ToUpperInvariant();
        if (!Roles.All.Contains(targetRoleCode))
        {
            return (false, 400, $"Vai trò '{request.Role}' không hợp lệ. Các vai trò được hỗ trợ: {string.Join(", ", Roles.All)}.", null);
        }

        await AdminMutationLock.WaitAsync(ct);
        try
        {
            await using var transaction = await _context.BeginTransactionAsync(ct);
            try
            {
                if (_context.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
                {
                    await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(990099);", ct);
                }

                var targetUser = await _context.Users
                    .Include(u => u.Role)
                    .Include(u => u.ApprovedByUser)
                    .FirstOrDefaultAsync(u => u.Id == id, ct);

                if (targetUser == null)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, 404, $"Không tìm thấy người dùng với ID: {id}.", null);
                }

                // Prevent admin from demoting themselves
                if (targetUser.Id == actorId && targetRoleCode != Roles.Admin)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, 400, "Quản trị viên không thể tự hạ quyền tài khoản của chính mình.", null);
                }

                // Prevent demoting the last active admin
                if (targetUser.Role.Code == Roles.Admin && targetRoleCode != Roles.Admin)
                {
                    if (await IsLastActiveAdminAsync(targetUser.Id, ct))
                    {
                        await transaction.RollbackAsync(ct);
                        return (false, 400, "Không thể hạ quyền quản trị viên (ADMIN) đang hoạt động cuối cùng của hệ thống.", null);
                    }
                }

                if (targetUser.Role.Code == targetRoleCode)
                {
                    await transaction.RollbackAsync(ct);
                    var currentDetail = await GetUserByIdAsync(id, ct);
                    return (true, 200, $"Tài khoản '{targetUser.Username}' hiện đã có vai trò '{targetRoleCode}'.", currentDetail);
                }

                var newRole = await _context.Roles.FirstOrDefaultAsync(r => r.Code == targetRoleCode, ct);
                if (newRole == null)
                {
                    await transaction.RollbackAsync(ct);
                    return (false, 400, $"Không tìm thấy vai trò '{targetRoleCode}' trong cơ sở dữ liệu.", null);
                }

                var oldRoleCode = targetUser.Role.Code;
                targetUser.RoleId = newRole.Id;
                targetUser.Role = newRole;
                targetUser.UpdatedAt = DateTime.UtcNow;

                int revokedAssignmentsCount = 0;
                // If user demoted/changed to a role that cannot hold lot assignments (e.g. USER or ADMIN)
                if (targetRoleCode != Roles.Manager && targetRoleCode != Roles.Staff)
                {
                    var activeAssignments = await _context.StaffLotAssignments
                        .Include(a => a.Lot)
                        .Where(a => a.UserId == targetUser.Id && a.IsActive)
                        .ToListAsync(ct);

                    var reason = string.IsNullOrWhiteSpace(request.Reason)
                        ? $"Tự động thu hồi do vai trò thay đổi từ {oldRoleCode} sang {targetRoleCode}."
                        : $"Tự động thu hồi do vai trò thay đổi sang {targetRoleCode}: {request.Reason.Trim()}";

                    foreach (var a in activeAssignments)
                    {
                        a.IsActive = false;
                        a.RevokedBy = actorId;
                        a.RevokedAt = DateTime.UtcNow;
                        a.RevocationReason = reason;
                        a.UpdatedAt = DateTime.UtcNow;
                        revokedAssignmentsCount++;

                        _context.AuditLogs.Add(new AuditLog
                        {
                            Action = "REVOKE_STAFF_LOT",
                            TargetEntity = "staff_lot_assignments",
                            TargetId = a.Id.ToString(),
                            OldValues = JsonSerializer.Serialize(new { a.Id, a.LotId, Status = "ACTIVE" }, JsonOptions),
                            NewValues = JsonSerializer.Serialize(new { Status = "REVOKED", RevocationReason = reason, RevokedBy = actorId }, JsonOptions),
                            ActorId = actorId,
                            IpAddress = ipAddress,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                // Revoke active refresh tokens so stale tokens cannot issue old claims
                var activeTokens = await _context.RefreshTokens
                    .Where(rt => rt.UserId == targetUser.Id && !rt.IsRevoked)
                    .ToListAsync(ct);

                foreach (var rt in activeTokens)
                {
                    rt.IsRevoked = true;
                    rt.RevokedAt = DateTime.UtcNow;
                }

                var audit = new AuditLog
                {
                    Action = "UPDATE_USER_ROLE",
                    TargetEntity = "users",
                    TargetId = targetUser.Id.ToString(),
                    OldValues = JsonSerializer.Serialize(new { Role = oldRoleCode }, JsonOptions),
                    NewValues = JsonSerializer.Serialize(new
                    {
                        Role = targetRoleCode,
                        request.Reason,
                        RevokedAssignmentsCount = revokedAssignmentsCount,
                        IpAddress = ipAddress
                    }, JsonOptions),
                    ActorId = actorId,
                    IpAddress = ipAddress,
                    CreatedAt = DateTime.UtcNow
                };
                _context.AuditLogs.Add(audit);

                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                _logger.LogInformation("Admin ID {AdminId} updated role of User ID {UserId} ({Username}) from {OldRole} to {NewRole}", actorId, targetUser.Id, targetUser.Username, oldRoleCode, targetRoleCode);

                var updatedDetail = await GetUserByIdAsync(id, ct);
                return (true, 200, $"Cập nhật vai trò tài khoản '{targetUser.Username}' thành '{targetRoleCode}' thành công.", updatedDetail);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Lỗi khi cập nhật vai trò người dùng ID {UserId}", id);
                throw;
            }
        }
        finally
        {
            AdminMutationLock.Release();
        }
    }

    private async Task<bool> IsLastActiveAdminAsync(long userId, CancellationToken ct)
    {
        var otherActiveAdminsCount = await _context.Users
            .CountAsync(u => u.Role.Code == Roles.Admin && u.IsActive && u.Id != userId, ct);

        return otherActiveAdminsCount == 0;
    }

    private static bool IsPostgresUniqueViolation(DbUpdateException ex, string constraintName)
    {
        if (ex.InnerException is PostgresException pgEx)
        {
            return pgEx.SqlState == PostgresErrorCodes.UniqueViolation &&
                   (string.IsNullOrEmpty(constraintName) || pgEx.ConstraintName?.Equals(constraintName, StringComparison.OrdinalIgnoreCase) == true);
        }

        var innerMsg = ex.InnerException?.Message ?? ex.Message;
        if (!innerMsg.Contains("23505"))
        {
            return false;
        }

        return string.IsNullOrEmpty(constraintName) || innerMsg.Contains(constraintName, StringComparison.OrdinalIgnoreCase);
    }

    private static AdminUserDetailDto MapToDetailDto(
        User user,
        List<AssignedLotSummaryDto> activeAssignments,
        int vehiclesCount,
        string? approvedByUsername = null)
    {
        return new AdminUserDetailDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role?.Code ?? string.Empty,
            IsActive = user.IsActive,
            IsEmailVerified = user.IsEmailVerified,
            ApprovalStatus = user.ApprovalStatus,
            ApprovedBy = user.ApprovedBy,
            ApprovedByUsername = approvedByUsername ?? user.ApprovedByUser?.Username,
            ApprovedAt = user.ApprovedAt,
            FailedLoginAttempts = user.FailedLoginAttempts,
            LockoutEndAt = user.LockoutEndAt,
            ActiveAssignmentsCount = activeAssignments.Count,
            VehiclesCount = vehiclesCount,
            ActiveAssignments = activeAssignments,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}
