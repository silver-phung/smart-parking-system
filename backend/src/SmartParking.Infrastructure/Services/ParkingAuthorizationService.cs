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

    public async Task<bool> HasLotAccessAsync(long userId, string userRole, int lotId, CancellationToken ct = default)
    {
        if (userRole == Roles.Admin)
        {
            return true;
        }

        if (userRole == Roles.Manager || userRole == Roles.Staff)
        {
            return await _context.StaffLotAssignments
                .AnyAsync(a => a.UserId == userId && a.LotId == lotId && a.IsActive, ct);
        }

        return false;
    }

    public async Task<List<int>> GetAssignedLotIdsAsync(long userId, string userRole, CancellationToken ct = default)
    {
        if (userRole == Roles.Admin)
        {
            return await _context.ParkingLots
                .Select(l => l.Id)
                .ToListAsync(ct);
        }

        if (userRole == Roles.Manager || userRole == Roles.Staff)
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
        if (userRole == Roles.Admin)
        {
            return true;
        }

        if (userRole == Roles.Manager)
        {
            return await HasLotAccessAsync(userId, userRole, lotId, ct);
        }

        return false;
    }

    public async Task<bool> CanManageSlotStatusAsync(long userId, string userRole, int lotId, CancellationToken ct = default)
    {
        if (userRole == Roles.Admin)
        {
            return true;
        }

        if (userRole == Roles.Manager)
        {
            return await HasLotAccessAsync(userId, userRole, lotId, ct);
        }

        return false;
    }

    public async Task<bool> CanOperateSessionOrDispatchAsync(long userId, string userRole, int lotId, CancellationToken ct = default)
    {
        if (userRole == Roles.Admin)
        {
            return true;
        }

        if (userRole == Roles.Manager || userRole == Roles.Staff)
        {
            return await HasLotAccessAsync(userId, userRole, lotId, ct);
        }

        return false;
    }
}
