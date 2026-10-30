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
    int ProposedSafetyBuffer = 5,
    int UnmatchedPhysicalOccupiedCount = 0,
    int MatchedPairsCount = 0,
    int ConfiguredCapacity = 0
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
        int unmatchedPhysicalOccupiedCount = 0;

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

            // Ô đỗ có cảm biến báo OCCUPIED nhưng chưa có phiên đỗ xe ACTIVE/PAID gắn vào
            unmatchedPhysicalOccupiedCount = await context.ParkingSlots
                .CountAsync(s => s.Floor.LotId == lotId &&
                                 s.PhysicalStatus == SlotPhysicalStatuses.Occupied &&
                                 !s.CurrentSessions.Any(sess => sess.Status == SessionStatuses.Active || sess.Status == SessionStatuses.Paid), ct);
        }
        else
        {
            // Sprint 3 Rule: An active lot with zero configured usable physical slots must NOT accept new Booking or Check-in.
            // Operational capacity is strictly 0. Never fall back to ConfiguredCapacity (e.g. 100000) to authorize vehicles.
            totalCapacity = 0;
        }

        if (lot == null)
        {
            lot = await context.ParkingLots.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lotId, ct);
        }
        var configuredCapacity = lot?.TotalCapacity ?? 0;

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
        // Công thức bảo tồn sức chứa không bỏ sót (chống undercount) và không đếm kép (chống double-count):
        // - Cặp Session-Slot đã ghép và cảm biến báo OCCUPIED (Matched): tính đúng 1 lần.
        // - Phiên đã check-in nhưng chưa ghép ô (UnassignedSessions): xe đang tìm chỗ/ở luồng di chuyển.
        // - Ô có cảm biến báo OCCUPIED nhưng chưa ghép phiên (UnmatchedPhysicalOccupied): xe không rõ danh tính hoặc lỗi cảm biến.
        // EffectiveOccupied = MatchedPairs + UnassignedSessions + UnmatchedPhysicalOccupied
        //                   = PhysicalOccupiedSlots + UnassignedSessionsCount
        //                   = ActiveSessionsCount + UnmatchedPhysicalOccupiedCount
        // Dependency: Phân tách riêng biệt giữa WALK_IN và FIXED phụ thuộc vào schema Zone (Zone entity/ZoneId) do Thức sở hữu;
        // khi chưa có Zone contract độc lập, áp dụng tính toán bảo thủ trên toàn bãi.
        int effectiveOccupied;
        int matchedPairsCount = 0;
        if (hasConfiguredSlots)
        {
            matchedPairsCount = physicalOccupiedSlots - unmatchedPhysicalOccupiedCount;
            effectiveOccupied = matchedPairsCount + unassignedSessionsCount + unmatchedPhysicalOccupiedCount;
        }
        else
        {
            effectiveOccupied = activeSessionsCount;
        }

        int totalOccupiedOrReserved = effectiveOccupied + pendingBookingsCount;
        int availableCapacity = Math.Max(0, totalCapacity - totalOccupiedOrReserved);

        // Proposed safety buffer (proposal only, non-blocking until Owner approves enforcement)
        const int proposedSafetyBuffer = 5;

        // 5. Diagnostics for discrepancy between sensor and backend sessions
        string? discrepancyNotes = null;
        if (hasConfiguredSlots && physicalOccupiedSlots != activeSessionsCount)
        {
            discrepancyNotes = $"Ghi nhận chênh lệch IoT: {physicalOccupiedSlots} ô vật lý báo có xe (OCCUPIED, trong đó {unmatchedPhysicalOccupiedCount} ô chưa khớp phiên), hệ thống có {activeSessionsCount} phiên đỗ xe (ACTIVE/PAID, trong đó {unassignedSessionsCount} xe chưa xác minh ô).";
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
            ProposedSafetyBuffer: proposedSafetyBuffer,
            UnmatchedPhysicalOccupiedCount: unmatchedPhysicalOccupiedCount,
            MatchedPairsCount: matchedPairsCount,
            ConfiguredCapacity: configuredCapacity
        );
    }
}
