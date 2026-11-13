using Microsoft.EntityFrameworkCore;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Domain.Constants;

namespace SmartParking.Infrastructure.Services;

public class ParkingAuthorizationService : IParkingAuthorizationService
{
    private readonly IApplicationDbContext _context;

    public ParkingAuthorizationService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> CanOperateAreaAsync(long userId, string userRole, int lotId, string duty, int? floorId, int? zoneId, CancellationToken ct = default)
    {
        var role = await ResolveEffectiveRoleAsync(userId, ct);
        if (role == Roles.Admin) return true;
        if (role == Roles.Manager) return await _context.StaffLotAssignments.AnyAsync(a => a.UserId == userId && a.LotId == lotId && a.IsActive, ct);
        if (role != Roles.Staff) return false;
        try
        {
            return await _context.StaffLotAssignments.AnyAsync(a => a.UserId == userId && a.LotId == lotId && a.IsActive &&
                (a.Duty == duty || a.Duty == StaffDuties.Both ||
                    ((duty == StaffDuties.GateIn || duty == StaffDuties.GateOut) && a.Duty == StaffDuties.Gate)) &&
                (a.FloorId == null || (floorId != null ? a.FloorId == floorId : zoneId == null ||
                    _context.ParkingZones.Any(z => z.Id == zoneId && z.FloorId == a.FloorId && z.LotId == lotId))) &&
                (a.ZoneId == null || (zoneId != null && a.ZoneId == zoneId)), ct);
        }
        catch (Exception ex) when (
            (ex is Npgsql.PostgresException pg && pg.SqlState == "42703") ||
            (ex.InnerException is Npgsql.PostgresException innerPg && innerPg.SqlState == "42703") ||
            ex.Message.Contains("floor_id", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("zone_id", StringComparison.OrdinalIgnoreCase))
        {
            return await _context.StaffLotAssignments.AnyAsync(a => a.UserId == userId && a.LotId == lotId && a.IsActive &&
                (a.Duty == duty || a.Duty == StaffDuties.Both ||
                    ((duty == StaffDuties.GateIn || duty == StaffDuties.GateOut) && a.Duty == StaffDuties.Gate)), ct);
        }
    }

    private async Task<string?> ResolveEffectiveRoleAsync(long userId, CancellationToken ct)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user != null)
        {
            if (!user.IsActive)
            {
                return null;
            }

            return user.Role?.Code;
        }

        return null;
    }

    public async Task<bool> HasLotAccessAsync(long userId, string userRole, int lotId, CancellationToken ct = default)
    {
        var effectiveRole = await ResolveEffectiveRoleAsync(userId, ct);
        if (effectiveRole == null)
        {
            return false;
        }

        if (effectiveRole == Roles.Admin)
        {
            return true;
        }

        if (effectiveRole == Roles.Manager || effectiveRole == Roles.Staff)
        {
            return await _context.StaffLotAssignments
                .AnyAsync(a => a.UserId == userId && a.LotId == lotId && a.IsActive, ct);
        }

        return false;
    }

    public async Task<List<int>> GetAssignedLotIdsAsync(long userId, string userRole, CancellationToken ct = default)
    {
        var effectiveRole = await ResolveEffectiveRoleAsync(userId, ct);
        if (effectiveRole == null)
        {
            return [];
        }

        if (effectiveRole == Roles.Admin)
        {
            return await _context.ParkingLots
                .Select(l => l.Id)
                .ToListAsync(ct);
        }

        if (effectiveRole == Roles.Manager || effectiveRole == Roles.Staff)
        {
            return await _context.StaffLotAssignments
                .Where(a => a.UserId == userId && a.IsActive)
                .Select(a => a.LotId)
                .Distinct()
                .ToListAsync(ct);
        }

        return [];
    }

    public async Task<bool> CanManageLotStatusAsync(long userId, string userRole, int lotId, CancellationToken ct = default)
    {
        var effectiveRole = await ResolveEffectiveRoleAsync(userId, ct);
        if (effectiveRole == null)
        {
            return false;
        }

        if (effectiveRole == Roles.Admin)
        {
            return true;
        }

        if (effectiveRole == Roles.Manager)
        {
            return await _context.StaffLotAssignments
                .AnyAsync(a => a.UserId == userId && a.LotId == lotId && a.IsActive, ct);
        }

        return false;
    }

    public async Task<bool> CanManageSlotStatusAsync(long userId, string userRole, int lotId, CancellationToken ct = default)
    {
        var effectiveRole = await ResolveEffectiveRoleAsync(userId, ct);
        if (effectiveRole == null)
        {
            return false;
        }

        if (effectiveRole == Roles.Admin)
        {
            return true;
        }

        if (effectiveRole == Roles.Manager)
        {
            return await _context.StaffLotAssignments
                .AnyAsync(a => a.UserId == userId && a.LotId == lotId && a.IsActive, ct);
        }

        return false;
    }

    public Task<bool> CanOperateGateAsync(long userId, string userRole, int lotId, CancellationToken ct = default)
        => CanOperateGateDutyAsync(userId, userRole, lotId, null, ct);

    public Task<bool> CanOperateGateInAsync(long userId, string userRole, int lotId, CancellationToken ct = default)
        => CanOperateGateDutyAsync(userId, userRole, lotId, StaffDuties.GateIn, ct);

    public Task<bool> CanOperateGateOutAsync(long userId, string userRole, int lotId, CancellationToken ct = default)
        => CanOperateGateDutyAsync(userId, userRole, lotId, StaffDuties.GateOut, ct);

    private async Task<bool> CanOperateGateDutyAsync(long userId, string userRole, int lotId, string? duty, CancellationToken ct)
    {
        var effectiveRole = await ResolveEffectiveRoleAsync(userId, ct);
        if (effectiveRole == null)
        {
            return false;
        }

        if (effectiveRole == Roles.Admin)
        {
            return true;
        }

        if (effectiveRole == Roles.Manager)
        {
            return await _context.StaffLotAssignments
                .AnyAsync(a => a.UserId == userId && a.LotId == lotId && a.IsActive, ct);
        }

        if (effectiveRole == Roles.Staff)
        {
            return await _context.StaffLotAssignments
                .AnyAsync(a => a.UserId == userId && a.LotId == lotId && a.IsActive &&
                               (a.Duty == StaffDuties.Gate || a.Duty == StaffDuties.Both || a.Duty == duty ||
                                   (duty == null && (a.Duty == StaffDuties.GateIn || a.Duty == StaffDuties.GateOut))), ct);
        }

        return false;
    }

    public async Task<bool> CanOperateSlotVerificationAsync(long userId, string userRole, int lotId, CancellationToken ct = default)
    {
        var effectiveRole = await ResolveEffectiveRoleAsync(userId, ct);
        if (effectiveRole == null)
        {
            return false;
        }

        if (effectiveRole == Roles.Admin)
        {
            return true;
        }

        if (effectiveRole == Roles.Manager)
        {
            return await _context.StaffLotAssignments
                .AnyAsync(a => a.UserId == userId && a.LotId == lotId && a.IsActive, ct);
        }

        if (effectiveRole == Roles.Staff)
        {
            return await _context.StaffLotAssignments
                .AnyAsync(a => a.UserId == userId && a.LotId == lotId && a.IsActive &&
                               (a.Duty == StaffDuties.SlotVerifier || a.Duty == StaffDuties.Both), ct);
        }

        return false;
    }

    public async Task<bool> CanOperateSessionOrDispatchAsync(long userId, string userRole, int lotId, CancellationToken ct = default)
    {
        return await CanOperateGateAsync(userId, userRole, lotId, ct) ||
               await CanOperateSlotVerificationAsync(userId, userRole, lotId, ct);
    }
}

