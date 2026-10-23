using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Common.Models;
using SmartParking.Application.DTOs.StaffAssignments;
using SmartParking.Domain.Constants;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Services;

public class StaffLotAssignmentService : IStaffLotAssignmentService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<StaffLotAssignmentService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    public StaffLotAssignmentService(IApplicationDbContext context, ILogger<StaffLotAssignmentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<(bool Success, string Message, StaffLotAssignmentDto? Data)> CreateAssignmentAsync(
        CreateStaffAssignmentRequestDto request,
        long actorId,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var targetUser = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, ct);

        if (targetUser == null)
        {
            return (false, $"Không tìm thấy người dùng với ID: {request.UserId}.", null);
        }

        if (!targetUser.IsActive)
        {
            return (false, $"Tài khoản '{targetUser.Username}' hiện đang bị vô hiệu hóa.", null);
        }

        if (targetUser.ApprovalStatus != "APPROVED")
        {
            return (false, $"Tài khoản '{targetUser.Username}' chưa được phê duyệt (Trạng thái: {targetUser.ApprovalStatus}).", null);
        }

        var roleCode = targetUser.Role.Code;
        if (roleCode != Roles.Manager && roleCode != Roles.Staff)
        {
            return (false, $"Chỉ có thể phân công bãi đỗ xe cho tài khoản có vai trò MANAGER hoặc STAFF. Vai trò hiện tại của '{targetUser.Username}' là: {roleCode}.", null);
        }

        var targetLot = await _context.ParkingLots
            .FirstOrDefaultAsync(l => l.Id == request.LotId, ct);

        if (targetLot == null)
        {
            return (false, $"Không tìm thấy bãi đỗ xe với ID: {request.LotId}.", null);
        }

        if (!targetLot.IsActive)
        {
            return (false, $"Bãi đỗ xe '{targetLot.Name}' hiện đang tạm đóng hoặc ngừng hoạt động.", null);
        }

        var hasActiveAssignment = await _context.StaffLotAssignments
            .AnyAsync(a => a.UserId == targetUser.Id && a.LotId == targetLot.Id && a.IsActive, ct);

        if (hasActiveAssignment)
        {
            return (false, $"Nhân sự '{targetUser.FullName}' ({targetUser.Username}) hiện đã được phân công tại bãi đỗ xe '{targetLot.Name}'.", null);
        }

        var actor = await _context.Users.FirstOrDefaultAsync(u => u.Id == actorId, ct);
        var actorUsername = actor?.Username ?? "admin";

        await using var transaction = await _context.BeginTransactionAsync(ct);
        try
        {
            var assignment = new StaffLotAssignment
            {
                UserId = targetUser.Id,
                LotId = targetLot.Id,
                AssignedBy = actorId,
                AssignedAt = DateTime.UtcNow,
                IsActive = true,
                Notes = request.Notes?.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.StaffLotAssignments.Add(assignment);
            await _context.SaveChangesAsync(ct);

            await LogAuditAsync(
                action: "ASSIGN_STAFF_LOT",
                targetEntity: "staff_lot_assignments",
                targetId: assignment.Id.ToString(),
                oldValues: null,
                newValues: new
                {
                    assignment.Id,
                    assignment.UserId,
                    targetUser.Username,
                    UserRole = roleCode,
                    assignment.LotId,
                    targetLot.Name,
                    assignment.AssignedBy,
                    IpAddress = ipAddress
                },
                actorId: actorId,
                ct: ct
            );

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            var dto = MapToDto(assignment, targetUser, targetLot, actorUsername, null);
            return (true, $"Phân công nhân sự '{targetUser.FullName}' vào bãi đỗ xe '{targetLot.Name}' thành công.", dto);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(ct);
            if (IsPostgresUniqueViolation(ex, "uq_staff_lot_assignments_active"))
            {
                return (false, $"Xung đột phân công: Nhân sự '{targetUser.FullName}' hiện đã có phân công đang hiệu lực tại bãi đỗ xe '{targetLot.Name}'.", null);
            }

            _logger.LogError(ex, "Lỗi cơ sở dữ liệu khi phân công nhân sự {UserId} vào bãi {LotId}", request.UserId, request.LotId);
            throw;
        }
    }

    public async Task<PagedResult<StaffLotAssignmentDto>> GetAssignmentsAsync(
        StaffAssignmentFilterDto filter,
        long actorId,
        string actorRole,
        CancellationToken ct = default)
    {
        var query = _context.StaffLotAssignments
            .AsNoTracking()
            .Include(a => a.User)
                .ThenInclude(u => u.Role)
            .Include(a => a.Lot)
            .Include(a => a.AssignedByUser)
            .Include(a => a.RevokedByUser)
            .AsQueryable();

        if (actorRole != Roles.Admin)
        {
            if (actorRole == Roles.Manager)
            {
                // Manager chỉ được xem danh sách phân công thuộc các bãi mà mình được phân công
                var managedLotIds = await _context.StaffLotAssignments
                    .Where(a => a.UserId == actorId && a.IsActive)
                    .Select(a => a.LotId)
                    .ToListAsync(ct);

                query = query.Where(a => managedLotIds.Contains(a.LotId));
            }
            else
            {
                return PagedResult<StaffLotAssignmentDto>.Create([], 0, filter.Page, filter.PageSize);
            }
        }

        if (filter.LotId.HasValue)
        {
            query = query.Where(a => a.LotId == filter.LotId.Value);
        }

        if (filter.UserId.HasValue)
        {
            query = query.Where(a => a.UserId == filter.UserId.Value);
        }

        if (filter.IsActive.HasValue)
        {
            query = query.Where(a => a.IsActive == filter.IsActive.Value);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(a => MapToDto(a, a.User, a.Lot, a.AssignedByUser.Username, a.RevokedByUser != null ? a.RevokedByUser.Username : null))
            .ToListAsync(ct);

        return PagedResult<StaffLotAssignmentDto>.Create(items, totalCount, filter.Page, filter.PageSize);
    }

    public async Task<StaffLotAssignmentDto?> GetAssignmentByIdAsync(
        long id,
        long actorId,
        string actorRole,
        CancellationToken ct = default)
    {
        var assignment = await _context.StaffLotAssignments
            .AsNoTracking()
            .Include(a => a.User)
                .ThenInclude(u => u.Role)
            .Include(a => a.Lot)
            .Include(a => a.AssignedByUser)
            .Include(a => a.RevokedByUser)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (assignment == null)
        {
            return null;
        }

        if (actorRole != Roles.Admin)
        {
            if (actorRole == Roles.Manager)
            {
                var isManaged = await _context.StaffLotAssignments
                    .AnyAsync(a => a.UserId == actorId && a.LotId == assignment.LotId && a.IsActive, ct);

                if (!isManaged)
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        return MapToDto(
            assignment,
            assignment.User,
            assignment.Lot,
            assignment.AssignedByUser.Username,
            assignment.RevokedByUser?.Username
        );
    }

    public async Task<List<AssignedLotSummaryDto>> GetMyAssignedLotsAsync(
        long userId,
        CancellationToken ct = default)
    {
        return await _context.StaffLotAssignments
            .AsNoTracking()
            .Include(a => a.Lot)
            .Where(a => a.UserId == userId && a.IsActive && a.Lot.IsActive)
            .OrderBy(a => a.Lot.Name)
            .Select(a => new AssignedLotSummaryDto
            {
                LotId = a.Lot.Id,
                LotName = a.Lot.Name,
                LotAddress = a.Lot.Address,
                IsActive = a.Lot.IsActive,
                AssignedAt = a.AssignedAt
            })
            .ToListAsync(ct);
    }

    public async Task<(bool Success, string Message)> RevokeAssignmentAsync(
        long id,
        RevokeStaffAssignmentRequestDto request,
        long actorId,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var assignment = await _context.StaffLotAssignments
            .Include(a => a.User)
            .Include(a => a.Lot)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (assignment == null)
        {
            return (false, $"Không tìm thấy bản ghi phân công với ID: {id}.");
        }

        if (!assignment.IsActive)
        {
            return (false, $"Phân công của nhân sự '{assignment.User.FullName}' tại bãi '{assignment.Lot.Name}' đã được thu hồi trước đó.");
        }

        await using var transaction = await _context.BeginTransactionAsync(ct);
        try
        {
            assignment.IsActive = false;
            assignment.RevokedBy = actorId;
            assignment.RevokedAt = DateTime.UtcNow;
            assignment.RevocationReason = request.Reason?.Trim();
            assignment.UpdatedAt = DateTime.UtcNow;

            await LogAuditAsync(
                action: "REVOKE_STAFF_LOT",
                targetEntity: "staff_lot_assignments",
                targetId: assignment.Id.ToString(),
                oldValues: new { Status = "ACTIVE", assignment.UserId, assignment.LotId },
                newValues: new
                {
                    Status = "REVOKED",
                    assignment.RevokedBy,
                    assignment.RevokedAt,
                    assignment.RevocationReason,
                    IpAddress = ipAddress
                },
                actorId: actorId,
                ct: ct
            );

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return (true, $"Thu hồi phân công của nhân sự '{assignment.User.FullName}' tại bãi '{assignment.Lot.Name}' thành công.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Lỗi khi thu hồi phân công nhân sự ID {AssignmentId}", id);
            throw;
        }
    }

    private static StaffLotAssignmentDto MapToDto(StaffLotAssignment a, User user, ParkingLot lot, string assignedByUsername, string? revokedByUsername)
    {
        return new StaffLotAssignmentDto
        {
            Id = a.Id,
            UserId = a.UserId,
            Username = user.Username,
            UserFullName = user.FullName,
            UserEmail = user.Email,
            UserRole = user.Role?.Code ?? string.Empty,
            LotId = a.LotId,
            LotName = lot.Name,
            AssignedBy = a.AssignedBy,
            AssignedByUsername = assignedByUsername,
            AssignedAt = a.AssignedAt,
            IsActive = a.IsActive,
            RevokedBy = a.RevokedBy,
            RevokedByUsername = revokedByUsername,
            RevokedAt = a.RevokedAt,
            RevocationReason = a.RevocationReason,
            Notes = a.Notes,
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt
        };
    }

    private static bool IsPostgresUniqueViolation(DbUpdateException ex, string constraintName)
    {
        if (ex.InnerException is Npgsql.PostgresException pgEx)
        {
            return pgEx.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation &&
                   (string.IsNullOrEmpty(constraintName) || pgEx.ConstraintName?.Equals(constraintName, StringComparison.OrdinalIgnoreCase) == true);
        }

        var innerMsg = ex.InnerException?.Message ?? ex.Message;
        return innerMsg.Contains("23505") || innerMsg.Contains(constraintName, StringComparison.OrdinalIgnoreCase);
    }

    private async Task LogAuditAsync(string action, string targetEntity, string targetId, object? oldValues, object? newValues, long? actorId, CancellationToken ct)
    {
        var audit = new AuditLog
        {
            Action = action,
            TargetEntity = targetEntity,
            TargetId = targetId,
            ActorId = actorId,
            OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues, JsonOptions) : null,
            NewValues = newValues != null ? JsonSerializer.Serialize(newValues, JsonOptions) : null,
            CreatedAt = DateTime.UtcNow
        };

        _context.AuditLogs.Add(audit);
    }
}
