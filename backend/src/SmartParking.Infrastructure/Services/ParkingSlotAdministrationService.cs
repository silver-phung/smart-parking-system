using Microsoft.EntityFrameworkCore;
using SmartParking.Application.Common.Helpers;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.DTOs.Operations;
using SmartParking.Application.DTOs.Parking;
using SmartParking.Domain.Constants;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Services;

public sealed class ParkingSlotAdministrationService(IApplicationDbContext db, IParkingManagementService parking,
    IParkingAuthorizationService auth, IParkingOperationsWriter writer)
{
    private async Task Access(int lotId, long? actorId, CancellationToken ct)
    {
        if (!actorId.HasValue || !await auth.CanManageSlotStatusAsync(actorId.Value, "", lotId, ct))
            throw new ParkingOperationException(403, "FORBIDDEN", "Bạn không có quyền quản lý ô đỗ tại bãi này.");
    }

    private async Task<ParkingSlot> Load(long id, long? actorId, CancellationToken ct)
    {
        var slot = await db.ParkingSlots.AsNoTracking().Include(s => s.Floor).Include(s => s.Zone).FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new ParkingOperationException(404, "NOT_FOUND", "Không tìm thấy ô đỗ.");
        await Access(slot.Floor.LotId, actorId, ct);
        return slot;
    }

    private async Task Guard(ParkingSlot slot, bool reducesCapacity, CancellationToken ct)
    {
        if (await db.ParkingSessions.AnyAsync(s => s.CurrentSlotId == slot.Id && (s.Status == SessionStatuses.Active || s.Status == SessionStatuses.Paid), ct) ||
            await db.ParkingDispatches.AnyAsync(d => d.AssignedSlotId == slot.Id && d.Status == DispatchStatuses.Assigned, ct))
            throw new ParkingOperationException(409, "SLOT_PROTECTED", "Ô đang có xe hoặc đã được giữ chỗ/điều phối. Không thể sửa, xóa hoặc đổi trạng thái.");
        if (reducesCapacity && slot.IsActive && slot.Floor.IsActive && slot.PhysicalStatus == SlotPhysicalStatuses.Empty &&
            slot.Zone.Type == "TRANSIENT")
        {
            // Bookings currently reserve capacity rather than a specific slot.
            var capacity = await ParkingLotCapacityHelper.CalculateAsync(db, slot.Floor.LotId, ct: ct);
            if (capacity.PendingBookingsCount > 0 && capacity.AvailableCapacity <= 0)
                throw new ParkingOperationException(409, "BOOKING_CAPACITY_RESERVED", "Chỗ trống còn lại đã dành cho booking. Thao tác này sẽ làm thiếu chỗ đã đặt trước.");
        }
    }

    public async Task<(bool Success, string Message, ParkingSlotDto? Slot)> Create(int floorId, CreateParkingSlotRequestDto request, long? actorId, CancellationToken ct)
    {
        var floor = await db.ParkingFloors.AsNoTracking().FirstOrDefaultAsync(f => f.Id == floorId, ct)
            ?? throw new ParkingOperationException(404, "NOT_FOUND", "Không tìm thấy tầng.");
        await Access(floor.LotId, actorId, ct);
        return await writer.ExecuteAsync(floor.LotId, () => parking.CreateSlotAsync(floorId, request, actorId, ct), ct);
    }

    public async Task<(bool Success, string Message, ParkingSlotDto? Slot)> Update(long id, UpdateParkingSlotRequestDto request, long? actorId, CancellationToken ct)
    {
        var slot = await Load(id, actorId, ct);
        return await writer.ExecuteAsync(slot.Floor.LotId, async () => {
            slot = await Load(id, actorId, ct);
            await Access(slot.Floor.LotId, actorId, ct);
            await Guard(slot, !request.IsActive, ct);
            return await parking.UpdateSlotAsync(id, request, actorId, ct);
        }, ct);
    }

    public async Task<(bool Success, string Message, ParkingSlotDto? Slot)> Status(long id, UpdateSlotPhysicalStatusRequestDto request, long? actorId, string actorRole, string? ip, CancellationToken ct)
    {
        var slot = await Load(id, actorId, ct);
        return await writer.ExecuteAsync(slot.Floor.LotId, async () => {
            slot = await Load(id, actorId, ct);
            await Access(slot.Floor.LotId, actorId, ct);
            await Guard(slot, request.PhysicalStatus != SlotPhysicalStatuses.Empty, ct);
            return await parking.UpdateSlotPhysicalStatusAsync(id, request, actorId, actorRole, ip, ct);
        }, ct);
    }

    public async Task<(bool Success, string Message)> Delete(long id, long? actorId, CancellationToken ct)
    {
        var slot = await Load(id, actorId, ct);
        return await writer.ExecuteAsync(slot.Floor.LotId, async () => {
            slot = await Load(id, actorId, ct);
            await Access(slot.Floor.LotId, actorId, ct);
            await Guard(slot, true, ct);
            return await parking.DeleteSlotAsync(id, actorId, ct);
        }, ct);
    }
}
