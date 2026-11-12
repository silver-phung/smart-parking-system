using Microsoft.EntityFrameworkCore;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Domain.Entities;

namespace SmartParking.Application.Common.Helpers;

public static class ParkingPolicyResolver
{
    // More specific zone policy takes precedence. Previously attached IDs never change.
    public static Task<ParkingPolicy?> ResolveAsync(IApplicationDbContext db, int lotId,
        int? zoneId, DateTime at, CancellationToken ct = default) => db.ParkingPolicies
        .AsNoTracking().Where(p => p.LotId == lotId && p.ApprovedAt != null &&
            p.EffectiveFrom <= at && (p.ZoneId == null || p.ZoneId == zoneId))
        .OrderByDescending(p => p.ZoneId.HasValue).ThenByDescending(p => p.EffectiveFrom)
        .ThenByDescending(p => p.Id).FirstOrDefaultAsync(ct);
}
