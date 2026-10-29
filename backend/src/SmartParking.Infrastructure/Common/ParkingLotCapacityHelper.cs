using Microsoft.EntityFrameworkCore;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Domain.Constants;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Common;

public record LotCapacityCalculation(
    int TotalCapacity,
    int ActiveSessionsCount,
    int PhysicalOccupiedSlotsCount,
    int PhysicalEmptySlotsCount,
    int PendingBookingsCount,
    int EffectiveOccupied,
    int TotalOccupiedOrReserved,
    int AvailableCapacity,
    bool HasConfiguredSlots,
    string? DiscrepancyNotes,
    int UnassignedSessionsCount = 0,
    int ProposedSafetyBuffer = 5
);

public static class ParkingLotCapacityHelper
{
    public static async Task<LotCapacityCalculation> CalculateAsync(
        IApplicationDbContext context,
        int lotId,
        ParkingLot? lot = null,
        CancellationToken ct = default)
    {
        // 1. Check if lot has configured ParkingSlot records
        var hasConfiguredSlots = await context.ParkingSlots
            .AnyAsync(s => s.Floor.LotId == lotId, ct);

        int totalCapacity;
        int physicalOccupiedSlots = 0;
        int physicalEmptySlots = 0;

        if (hasConfiguredSlots)
        {
            // Authoritative capacity derives from eligible/usable physical slots on active floors.
            // MAINTENANCE, UNAVAILABLE, and inactive slots/floors must not count as usable capacity.
            // When all slots are maintenance/unavailable, totalCapacity is 0 (NO fallback to lot.TotalCapacity).
            totalCapacity = await context.ParkingSlots
                .CountAsync(s => s.Floor.LotId == lotId && s.IsActive && s.Floor.IsActive &&
                                 s.PhysicalStatus != SlotPhysicalStatuses.Maintenance &&
                                 s.PhysicalStatus != SlotPhysicalStatuses.Unavailable, ct);

            physicalOccupiedSlots = await context.ParkingSlots
                .CountAsync(s => s.Floor.LotId == lotId && s.PhysicalStatus == SlotPhysicalStatuses.Occupied, ct);

            physicalEmptySlots = await context.ParkingSlots
                .CountAsync(s => s.Floor.LotId == lotId && s.PhysicalStatus == SlotPhysicalStatuses.Empty && s.IsActive && s.Floor.IsActive, ct);
        }
        else
        {
            // If lot has NO configured ParkingSlot records, allow existing TotalCapacity fallback
            if (lot == null)
            {
                lot = await context.ParkingLots.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lotId, ct);
            }
            totalCapacity = lot?.TotalCapacity ?? 0;
        }

        // 2. Active admitted vehicle sessions (vehicles checked in, inside or parking)
        var activeSessionsCount = await context.ParkingSessions
            .CountAsync(s => s.LotId == lotId && (s.Status == SessionStatuses.Active || s.Status == SessionStatuses.Paid), ct);

        // 2b. Admitted sessions whose vehicles do NOT yet occupy a verified physical OCCUPIED slot (in queue / looking for slot)
        var unassignedSessionsCount = await context.ParkingSessions
            .CountAsync(s => s.LotId == lotId &&
                             (s.Status == SessionStatuses.Active || s.Status == SessionStatuses.Paid) &&
                             (s.CurrentSlotId == null || s.CurrentSlot!.PhysicalStatus != SlotPhysicalStatuses.Occupied), ct);

        // 3. Active unconsumed reservation holds (only PENDING bookings await check-in; CHECKED_IN/CANCELLED/EXPIRED excluded)
        var pendingBookingsCount = await context.Bookings
            .CountAsync(b => b.LotId == lotId && b.Status == BookingStatuses.Pending, ct);

        // 4. Conservative IoT Capacity Guard:
        // Used = max(ActiveOrPaidSessions, PhysicalOccupiedSlots) + PendingBookingHolds
        // Available = max(0, UsableCapacity - Used)
        int effectiveOccupied = Math.Max(activeSessionsCount, physicalOccupiedSlots);
        int totalOccupiedOrReserved = effectiveOccupied + pendingBookingsCount;
        int availableCapacity = Math.Max(0, totalCapacity - totalOccupiedOrReserved);

        // Proposed safety buffer (proposal only, non-blocking until Owner approves enforcement)
        const int proposedSafetyBuffer = 5;

        // 5. Diagnostics for discrepancy between sensor and backend sessions
        string? discrepancyNotes = null;
        if (hasConfiguredSlots && physicalOccupiedSlots != activeSessionsCount)
        {
            discrepancyNotes = $"Ghi nhận chênh lệch IoT: {physicalOccupiedSlots} ô vật lý báo có xe (OCCUPIED), hệ thống đang có {activeSessionsCount} phiên đỗ xe (ACTIVE/PAID).";
        }

        return new LotCapacityCalculation(
            TotalCapacity: totalCapacity,
            ActiveSessionsCount: activeSessionsCount,
            PhysicalOccupiedSlotsCount: physicalOccupiedSlots,
            PhysicalEmptySlotsCount: physicalEmptySlots,
            PendingBookingsCount: pendingBookingsCount,
            EffectiveOccupied: effectiveOccupied,
            TotalOccupiedOrReserved: totalOccupiedOrReserved,
            AvailableCapacity: availableCapacity,
            HasConfiguredSlots: hasConfiguredSlots,
            DiscrepancyNotes: discrepancyNotes,
            UnassignedSessionsCount: unassignedSessionsCount,
            ProposedSafetyBuffer: proposedSafetyBuffer
        );
    }
}
