using Microsoft.EntityFrameworkCore;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Common.Helpers;
using SmartParking.Domain.Constants;

namespace SmartParking.Infrastructure.Services;

public sealed class ParkingSceneService(IApplicationDbContext db, IParkingAuthorizationService auth)
{
    public async Task<object?> Read(int lotId, long? actor, string role, CancellationToken ct)
    {
        var lot = await db.ParkingLots.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lotId, ct);
        if (lot == null) return null;
        // Resolve the current account rather than trusting an old token's role.
        var effectiveRole = actor.HasValue ? await db.Users.AsNoTracking().Where(u => u.Id == actor.Value && u.IsActive)
            .Select(u => u.Role.Code).FirstOrDefaultAsync(ct) : null;
        if (effectiveRole == null) actor = null;
        role = effectiveRole ?? "";
        var internalAccess = actor.HasValue && (role is Roles.Admin or Roles.Manager or Roles.Staff) &&
            await auth.HasLotAccessAsync(actor.Value, role, lotId, ct);
        // Public/customer callers must not see lots that are closed.
        if (!lot.IsActive && !internalAccess) return null;
        var floors = await db.ParkingFloors.AsNoTracking().Include(f => f.Zone)
            .Where(f => f.LotId == lotId).OrderBy(f => f.FloorNumber).ThenBy(f => f.Id).ToListAsync(ct);
        // Plate/session visibility per floor: Admin/Manager lot-wide, Staff only inside their verifier floor/zone scope.
        var privateFloors = new HashSet<int>();
        foreach (var f in floors)
        {
            if (!internalAccess) continue;
            if (role != Roles.Staff || await auth.CanOperateAreaAsync(actor!.Value, role, lotId, StaffDuties.SlotVerifier, f.Id, f.ZoneId, ct))
                privateFloors.Add(f.Id);
        }
        var slots = await db.ParkingSlots.AsNoTracking().Where(s => s.Floor.LotId == lotId)
            .OrderBy(s => s.SlotCode).ThenBy(s => s.Id).ToListAsync(ct);
        var sessions = await db.ParkingSessions.AsNoTracking()
            .Where(s => s.LotId == lotId && s.CurrentSlotId != null && (s.Status == "ACTIVE" || s.Status == "PAID"))
            .Select(s => new { s.Id, SlotId = s.CurrentSlotId!.Value, s.PlateNumber,
                IsMine = actor.HasValue && ((s.Vehicle != null && s.Vehicle.UserId == actor.Value) || (s.Booking != null && s.Booking.UserId == actor.Value)) })
            .ToListAsync(ct);
        var occupied = sessions.GroupBy(s => s.SlotId).ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.Id).First());
        var held = (await db.ParkingDispatches.AsNoTracking().Where(d => d.Session.LotId == lotId && d.Status == "ASSIGNED")
            .Select(d => d.AssignedSlotId).ToListAsync(ct)).ToHashSet();
        var capacity = await ParkingLotCapacityHelper.CalculateAsync(db, lotId, ct: ct);
        return new {
            lot.Id, lot.Name, lot.Address, lot.IsActive, GeneratedAtUtc = DateTime.UtcNow,
            Capacity = new { capacity.TotalCapacity, capacity.AvailableCapacity, capacity.ActiveSessionsCount, capacity.PendingBookingsCount },
            CanManage = actor.HasValue && await auth.CanManageSlotStatusAsync(actor.Value, role, lotId, ct),
            CanEnter = actor.HasValue && await auth.CanOperateGateInAsync(actor.Value, role, lotId, ct),
            CanExit = actor.HasValue && await auth.CanOperateGateOutAsync(actor.Value, role, lotId, ct),
            CanVerify = actor.HasValue && await auth.CanOperateSlotVerificationAsync(actor.Value, role, lotId, ct),
            Floors = floors.Select(f => new {
                f.Id, f.Name, f.FloorNumber, f.IsActive, CanVerify = privateFloors.Contains(f.Id),
                Zone = f.Zone == null ? null : new { f.Zone.Id, f.Zone.Name, f.Zone.Code, f.Zone.Type, f.Zone.IsActive },
                Slots = slots.Where(s => s.FloorId == f.Id).Select(s => {
                    occupied.TryGetValue(s.Id, out var session);
                    var status = session != null ? "OCCUPIED" : held.Contains(s.Id) ? "RESERVED" : !s.IsActive || !f.IsActive ? "UNAVAILABLE" : s.PhysicalStatus;
                    return new { s.Id, Code = s.SlotCode, Status = status, s.PhysicalStatus, s.IsActive,
                        IsAvailable = lot.IsActive && f.IsActive && s.IsActive && status == "EMPTY" && (f.Zone == null || f.Zone.IsActive),
                        IsProtected = session != null || held.Contains(s.Id),
                        Plate = session != null && (privateFloors.Contains(f.Id) || session.IsMine) ? session.PlateNumber : null,
                        SessionId = session != null && (privateFloors.Contains(f.Id) || session.IsMine) ? (long?)session.Id : null,
                        IsMine = session?.IsMine == true, s.PositionX, s.PositionY, s.PositionZ };
                }).ToList()
            }).ToList()
        };
    }
}
