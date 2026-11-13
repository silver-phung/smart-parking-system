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

    private Task<User?> GetAssignmentActorAsync(long actorId, CancellationToken ct) => _context.Users
        .AsNoTracking().Include(u => u.Role)
        .FirstOrDefaultAsync(u => u.Id == actorId && u.IsActive && u.ApprovalStatus == "APPROVED" &&
            (u.Role.Code == Roles.Admin || u.Role.Code == Roles.Manager), ct);

    private Task<bool> ManagesLotAsync(long actorId, int lotId, CancellationToken ct) => _context.StaffLotAssignments
        .AnyAsync(a => a.UserId == actorId && a.LotId == lotId && a.IsActive, ct);

    public async Task<PagedResult<StaffCandidateDto>> GetCandidatesAsync(
        StaffCandidateFilterDto filter, long actorId, CancellationToken ct = default)
    {
        if (await GetAssignmentActorAsync(actorId, ct) == null)
            return PagedResult<StaffCandidateDto>.Create([], 0, filter.Page, filter.PageSize);
        var query = _context.Users.AsNoTracking().Where(u => u.Role.Code == Roles.Staff &&
            u.IsActive && u.ApprovalStatus == "APPROVED");
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(u => u.Username.ToLower().Contains(search) ||
                u.FullName.ToLower().Contains(search) || u.Email.ToLower().Contains(search));
        }
        var count = await query.CountAsync(ct);
        var items = await query.OrderBy(u => u.FullName).ThenBy(u => u.Id)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(u => new StaffCandidateDto { Id = u.Id, Username = u.Username, FullName = u.FullName, Email = u.Email }).ToListAsync(ct);
        return PagedResult<StaffCandidateDto>.Create(items, count, filter.Page, filter.PageSize);
    }

    public async Task<AssignmentLocationsDto?> GetLocationsAsync(int lotId, long actorId, CancellationToken ct = default)
    {
        var actor = await GetAssignmentActorAsync(actorId, ct);
        if (actor == null || (actor.Role.Code == Roles.Manager && !await ManagesLotAsync(actorId, lotId, ct))) return null;
        if (!await _context.ParkingLots.AnyAsync(l => l.Id == lotId && l.IsActive, ct)) return null;
        var floors = await _context.ParkingFloors.AsNoTracking().Include(f => f.Zone)
            .Where(f => f.LotId == lotId && f.IsActive).OrderBy(f => f.FloorNumber).ThenBy(f => f.Id).ToListAsync(ct);
        return new AssignmentLocationsDto(floors.Select(f => new AssignmentFloorDto(f.Id, f.Name,
            f.Zone is { IsActive: true } z && z.LotId == lotId ? [new(z.Id, z.Name, z.Code)] : [])).ToList());
    }

    public async Task<(bool Success, string Message, StaffLotAssignmentDto? Data)> CreateAssignmentAsync(
        CreateStaffAssignmentRequestDto request,
        long actorId,
        string? ipAddress,
        CancellationToken ct = default)
    {
        var actor = await GetAssignmentActorAsync(actorId, ct);
        if (actor == null || (actor.Role.Code == Roles.Manager && !await ManagesLotAsync(actorId, request.LotId, ct)))
            return (false, "Bạn không có quyền phân công nhân viên tại bãi này.", null);
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
        if (roleCode != Roles.Staff)
        {
            return (false, "Chỉ có thể phân công cho tài khoản STAFF.", null);
        }

        var duty = string.IsNullOrWhiteSpace(request.Duty)
            ? StaffDuties.GateIn
            : request.Duty.Trim().ToUpperInvariant();

        if (!StaffDuties.IsAssignable(duty))
        {
            return (false, "Phân công không hợp lệ. Chọn trực cổng hoặc trực khu vực.", null);
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

        if (request.FloorId is not > 0)
            return (false, "Chọn tầng được phân công.", null);
        var floor = await _context.ParkingFloors.FirstOrDefaultAsync(f => f.Id == request.FloorId && f.LotId == request.LotId, ct);
        if (floor == null || !floor.IsActive)
            return (false, "Tầng phải đang hoạt động và thuộc đúng bãi được phân công.", null);
        var isArea = duty == StaffDuties.SlotVerifier;
        var zone = isArea ? await _context.ParkingZones.FirstOrDefaultAsync(z => z.Id == request.ZoneId && z.FloorId == floor.Id && z.LotId == request.LotId && z.IsActive, ct) : null;
        if (isArea && zone == null)
            return (false, "Tầng và khu vực phải đang hoạt động, thuộc đúng bãi và đúng quan hệ tầng–khu.", null);
        if (!isArea && request.ZoneId.HasValue)
            return (false, "Phân công trực cổng chỉ chọn tầng, không chọn khu vực.", null);

        var hasActiveAssignment = await _context.StaffLotAssignments
            .AnyAsync(a => a.UserId == targetUser.Id && a.LotId == targetLot.Id && a.IsActive, ct);

        if (hasActiveAssignment)
        {
            return (false, $"Nhân sự '{targetUser.FullName}' ({targetUser.Username}) hiện đã được phân công tại bãi đỗ xe '{targetLot.Name}'.", null);
        }

        var actorUsername = actor.Username;

        await using var transaction = await _context.BeginTransactionAsync(ct);
        try
        {
            var assignment = new StaffLotAssignment
            {
                UserId = targetUser.Id,
                LotId = targetLot.Id,
                FloorId = floor.Id,
                ZoneId = isArea ? zone!.Id : null,
                Floor = floor,
                Zone = zone,
                Duty = duty,
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
                    assignment.FloorId,
                    assignment.ZoneId,
                    assignment.Duty,
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
        var actor = await GetAssignmentActorAsync(actorId, ct);
        if (actor == null)
            return PagedResult<StaffLotAssignmentDto>.Create([], 0, filter.Page, filter.PageSize);

        var baseQuery = _context.StaffLotAssignments
            .AsNoTracking()
            .Where(a => a.User.Role.Code == Roles.Staff);

        if (actor.Role.Code != Roles.Admin)
        {
            if (actor.Role.Code == Roles.Manager)
            {
                // Manager chỉ được xem danh sách phân công thuộc các bãi mà mình được phân công
                var managedLotIds = await _context.StaffLotAssignments
                    .Where(a => a.UserId == actorId && a.IsActive)
                    .Select(a => a.LotId)
                    .ToListAsync(ct);

                baseQuery = baseQuery.Where(a => managedLotIds.Contains(a.LotId));
            }
            else
            {
                return PagedResult<StaffLotAssignmentDto>.Create([], 0, filter.Page, filter.PageSize);
            }
        }

        if (filter.LotId.HasValue)
        {
            baseQuery = baseQuery.Where(a => a.LotId == filter.LotId.Value);
        }

        if (filter.UserId.HasValue)
        {
            baseQuery = baseQuery.Where(a => a.UserId == filter.UserId.Value);
        }

        if (filter.IsActive.HasValue)
        {
            baseQuery = baseQuery.Where(a => a.IsActive == filter.IsActive.Value);
        }

        var totalCount = await baseQuery.CountAsync(ct);

        try
        {
            var items = await baseQuery
                .Include(a => a.User)
                    .ThenInclude(u => u.Role)
                .Include(a => a.Lot)
                .Include(a => a.Floor)
                .Include(a => a.Zone)
                .Include(a => a.AssignedByUser)
                .Include(a => a.RevokedByUser)
                .OrderByDescending(a => a.CreatedAt)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(a => MapToDto(a, a.User, a.Lot, a.AssignedByUser.Username, a.RevokedByUser != null ? a.RevokedByUser.Username : null))
                .ToListAsync(ct);

            return PagedResult<StaffLotAssignmentDto>.Create(items, totalCount, filter.Page, filter.PageSize);
        }
        catch (Exception ex) when (IsMissingFloorOrZoneColumn(ex))
        {
            var fallbackItems = await baseQuery
                .OrderByDescending(a => a.CreatedAt)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(a => new StaffLotAssignmentDto
                {
                    Id = a.Id,
                    UserId = a.UserId,
                    Username = a.User.Username,
                    UserFullName = a.User.FullName,
                    UserEmail = a.User.Email,
                    UserRole = a.User.Role != null ? a.User.Role.Code : string.Empty,
                    LotId = a.LotId,
                    LotName = a.Lot.Name,
                    AssignedBy = a.AssignedBy,
                    AssignedByUsername = a.AssignedByUser.Username,
                    AssignedAt = a.AssignedAt,
                    IsActive = a.IsActive,
                    Duty = a.Duty,
                    RevokedBy = a.RevokedBy,
                    RevokedByUsername = a.RevokedByUser != null ? a.RevokedByUser.Username : null,
                    RevokedAt = a.RevokedAt,
                    RevocationReason = a.RevocationReason,
                    Notes = a.Notes,
                    CreatedAt = a.CreatedAt,
                    UpdatedAt = a.UpdatedAt
                })
                .ToListAsync(ct);

            return PagedResult<StaffLotAssignmentDto>.Create(fallbackItems, totalCount, filter.Page, filter.PageSize);
        }
    }

    public async Task<StaffLotAssignmentDto?> GetAssignmentByIdAsync(
        long id,
        long actorId,
        string actorRole,
        CancellationToken ct = default)
    {
        var actor = await GetAssignmentActorAsync(actorId, ct);
        if (actor == null) return null;

        try
        {
            var assignment = await _context.StaffLotAssignments
                .AsNoTracking()
                .Include(a => a.User)
                    .ThenInclude(u => u.Role)
                .Include(a => a.Lot)
                .Include(a => a.Floor)
                .Include(a => a.Zone)
                .Include(a => a.AssignedByUser)
                .Include(a => a.RevokedByUser)
                .FirstOrDefaultAsync(a => a.Id == id, ct);

            if (assignment == null || assignment.User.Role.Code != Roles.Staff)
            {
                return null;
            }

            if (actor.Role.Code != Roles.Admin)
            {
                if (actor.Role.Code == Roles.Manager)
                {
                    var isManaged = await _context.StaffLotAssignments
                        .AnyAsync(a => a.UserId == actorId && a.LotId == assignment.LotId && a.IsActive, ct);

                    if (!isManaged) return null;
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
        catch (Exception ex) when (IsMissingFloorOrZoneColumn(ex))
        {
            var dto = await _context.StaffLotAssignments
                .AsNoTracking()
                .Where(a => a.Id == id && a.User.Role.Code == Roles.Staff)
                .Select(a => new StaffLotAssignmentDto
                {
                    Id = a.Id,
                    UserId = a.UserId,
                    Username = a.User.Username,
                    UserFullName = a.User.FullName,
                    UserEmail = a.User.Email,
                    UserRole = a.User.Role != null ? a.User.Role.Code : string.Empty,
                    LotId = a.LotId,
                    LotName = a.Lot.Name,
                    AssignedBy = a.AssignedBy,
                    AssignedByUsername = a.AssignedByUser.Username,
                    AssignedAt = a.AssignedAt,
                    IsActive = a.IsActive,
                    Duty = a.Duty,
                    RevokedBy = a.RevokedBy,
                    RevokedByUsername = a.RevokedByUser != null ? a.RevokedByUser.Username : null,
                    RevokedAt = a.RevokedAt,
                    RevocationReason = a.RevocationReason,
                    Notes = a.Notes,
                    CreatedAt = a.CreatedAt,
                    UpdatedAt = a.UpdatedAt
                })
                .FirstOrDefaultAsync(ct);

            if (dto == null) return null;
            if (actor.Role.Code != Roles.Admin)
            {
                if (actor.Role.Code != Roles.Manager || !await _context.StaffLotAssignments.AnyAsync(a => a.UserId == actorId && a.LotId == dto.LotId && a.IsActive, ct))
                    return null;
            }
            return dto;
        }
    }

    public async Task<List<AssignedLotSummaryDto>> GetMyAssignedLotsAsync(
        long userId,
        CancellationToken ct = default)
    {
        return await _context.StaffLotAssignments
            .AsNoTracking()
            .Include(a => a.Lot)
            .Where(a => a.UserId == userId && a.IsActive)
            .OrderBy(a => a.Lot.Name)
            .Select(a => new AssignedLotSummaryDto
            {
                LotId = a.Lot.Id,
                LotName = a.Lot.Name,
                LotAddress = a.Lot.Address,
                IsActive = a.Lot.IsActive,
                AssignedAt = a.AssignedAt,
                Duty = a.Duty
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
        var actor = await GetAssignmentActorAsync(actorId, ct);
        if (actor == null) return (false, "Bạn không có quyền thu hồi phân công.");
        var assignment = await _context.StaffLotAssignments
            .Include(a => a.User)
                .ThenInclude(u => u.Role)
            .Include(a => a.Lot)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (assignment == null)
        {
            return (false, $"Không tìm thấy bản ghi phân công với ID: {id}.");
        }

        if (actor.Role.Code == Roles.Manager && !await ManagesLotAsync(actorId, assignment.LotId, ct))
            return (false, "Bạn không có quyền thu hồi phân công tại bãi này.");
        if (assignment.User.Role.Code != Roles.Staff)
            return (false, "Chỉ xử lý phân công cho tài khoản STAFF trong chức năng này.");

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
            FloorId = a.FloorId,
            FloorName = a.Floor?.Name,
            ZoneId = a.ZoneId,
            ZoneName = a.Zone?.Name,
            AssignedBy = a.AssignedBy,
            AssignedByUsername = assignedByUsername,
            AssignedAt = a.AssignedAt,
            IsActive = a.IsActive,
            Duty = a.Duty,
            RevokedBy = a.RevokedBy,
            RevokedByUsername = revokedByUsername,
            RevokedAt = a.RevokedAt,
            RevocationReason = a.RevocationReason,
            Notes = a.Notes,
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt
        };
    }

    private static bool IsMissingFloorOrZoneColumn(Exception ex)
    {
        if (ex is Npgsql.PostgresException pgEx && pgEx.SqlState == "42703")
            return true;
        if (ex.InnerException is Npgsql.PostgresException innerPg && innerPg.SqlState == "42703")
            return true;
        var msg = ex.InnerException?.Message ?? ex.Message;
        return msg.Contains("floor_id", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("zone_id", StringComparison.OrdinalIgnoreCase) ||
               msg.Contains("42703", StringComparison.OrdinalIgnoreCase);
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
