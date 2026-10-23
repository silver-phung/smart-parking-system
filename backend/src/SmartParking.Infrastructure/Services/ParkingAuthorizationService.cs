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

    private async Task<string?> ResolveEffectiveRoleAsync(long userId, string claimRole, CancellationToken ct)
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

            return user.Role?.Code ?? claimRole;
        }

        return claimRole;
    }

    public async Task<bool> HasLotAccessAsync(long userId, string userRole, int lotId, CancellationToken ct = default)
    {
        var effectiveRole = await ResolveEffectiveRoleAsync(userId, userRole, ct);
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
        var effectiveRole = await ResolveEffectiveRoleAsync(userId, userRole, ct);
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
        var effectiveRole = await ResolveEffectiveRoleAsync(userId, userRole, ct);
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
        var effectiveRole = await ResolveEffectiveRoleAsync(userId, userRole, ct);
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

    public async Task<bool> CanOperateSessionOrDispatchAsync(long userId, string userRole, int lotId, CancellationToken ct = default)
    {
        var effectiveRole = await ResolveEffectiveRoleAsync(userId, userRole, ct);
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
}
