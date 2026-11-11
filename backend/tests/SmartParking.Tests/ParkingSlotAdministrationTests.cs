using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.Application.Common.Helpers;
using SmartParking.Application.DTOs.Operations;
using SmartParking.Application.DTOs.Parking;
using SmartParking.Domain.Entities;
using SmartParking.Infrastructure.Services;
using SmartParking.Persistence.Repositories;

namespace SmartParking.Tests;

public class ParkingSlotAdministrationTests
{
    private static async Task<(TestSmartParkingDbContext Db, ParkingSlotAdministrationService Service)> Setup()
    {
        var db = await TestDbHelper.CreateInMemoryDbContextAsync();
        db.Users.AddRange(new User { Id = 1, Username = "admin", FullName = "Admin", Email = "admin@test", PasswordHash = "x", RoleId = 1 },
            new User { Id = 2, Username = "manager", FullName = "Manager", Email = "manager@test", PasswordHash = "x", RoleId = 2 },
            new User { Id = 3, Username = "staff", FullName = "Staff", Email = "staff@test", PasswordHash = "x", RoleId = 3 });
        db.ParkingLots.AddRange(new ParkingLot { Id = 1, Name = "One" }, new ParkingLot { Id = 2, Name = "Other" });
        db.ParkingFloors.AddRange(new ParkingFloor { Id = 1, LotId = 1, Name = "Floor" }, new ParkingFloor { Id = 2, LotId = 2, Name = "Other floor" });
        db.ParkingSlots.AddRange(new ParkingSlot { Id = 1, FloorId = 1, SlotCode = "A1" }, new ParkingSlot { Id = 2, FloorId = 2, SlotCode = "B1" });
        db.StaffLotAssignments.Add(new StaffLotAssignment { UserId = 2, LotId = 1, AssignedBy = 1, IsActive = true });
        await db.SaveChangesAsync();
        var service = new ParkingSlotAdministrationService(db, new ParkingManagementService(db, NullLogger<ParkingManagementService>.Instance), new ParkingAuthorizationService(db), new ParkingOperationsWriter(db));
        return (db, service);
    }

    [Fact]
    public async Task ManagerCanCrudOwnSlotsButNotAnotherLotAndStaffCannotWrite()
    {
        var (db, service) = await Setup(); await using var dispose = db;
        var created = await service.Create(1, new() { SlotCode = "A2" }, 2, default);
        Assert.True(created.Success);
        var changed = await service.Update(created.Slot!.Id, new() { SlotCode = "A2-NEW", IsActive = true }, 2, default);
        Assert.True(changed.Success);
        Assert.True((await service.Delete(created.Slot.Id, 2, default)).Success);
        Assert.Equal(403, (await Assert.ThrowsAsync<ParkingOperationException>(() => service.Create(2, new() { SlotCode = "B2" }, 2, default))).Status);
        Assert.Equal(403, (await Assert.ThrowsAsync<ParkingOperationException>(() => service.Status(2, new() { PhysicalStatus = "MAINTENANCE" }, 2, "ADMIN", null, default))).Status);
        Assert.Equal(403, (await Assert.ThrowsAsync<ParkingOperationException>(() => service.Delete(1, 3, default))).Status);
    }

    [Theory]
    [InlineData("RESERVED")]
    [InlineData("OCCUPIED")]
    [InlineData("MAINTENANCE")]
    [InlineData("EMPTY")]
    public async Task AllowsManualStatesOnlyWhenSlotIsUnprotected(string status)
    {
        var (db, service) = await Setup(); await using var dispose = db;
        Assert.True((await service.Status(1, new() { PhysicalStatus = status, Reason = "Đối soát" }, 1, "ADMIN", null, default)).Success);
        Assert.Equal(status, (await db.ParkingSlots.FindAsync(1L))!.PhysicalStatus);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActiveSessionOrDispatchProtectsEveryMutationEvenIfPhysicalStatusIsEmpty(bool dispatch)
    {
        var (db, service) = await Setup(); await using var dispose = db;
        db.ParkingSessions.Add(new ParkingSession { Id = 1, LotId = 1, PlateNumber = "51F12345", Status = "ACTIVE", CurrentSlotId = dispatch ? null : 1 });
        if (dispatch) db.ParkingDispatches.Add(new ParkingDispatch { Id = 1, SessionId = 1, AssignedSlotId = 1, Status = "ASSIGNED" });
        await db.SaveChangesAsync();
        Assert.Equal("SLOT_PROTECTED", (await Assert.ThrowsAsync<ParkingOperationException>(() => service.Status(1, new() { PhysicalStatus = "EMPTY" }, 1, "ADMIN", null, default))).Code);
        Assert.Equal("SLOT_PROTECTED", (await Assert.ThrowsAsync<ParkingOperationException>(() => service.Update(1, new() { SlotCode = "RENAMED", IsActive = true }, 1, default))).Code);
        Assert.Equal("SLOT_PROTECTED", (await Assert.ThrowsAsync<ParkingOperationException>(() => service.Delete(1, 1, default))).Code);
        var query = await new ParkingManagementService(db, NullLogger<ParkingManagementService>.Instance).GetSlotsByFloorIdAsync(1, new());
        Assert.True(query.Items[0].IsProtected); Assert.False(query.Items[0].IsAvailable);
    }

    [Fact]
    public async Task PendingBookingProtectsRemainingCapacityAndManualHoldDoesNotSellCapacity()
    {
        var (db, service) = await Setup(); await using var dispose = db;
        db.Vehicles.Add(new Vehicle { Id = 1, UserId = 3, PlateNumber = "51F12345" });
        db.Bookings.Add(new Booking { Id = 1, LotId = 1, UserId = 3, VehicleId = 1, PlateNumber = "51F12345", Status = "PENDING", StartTime = DateTime.UtcNow.AddMinutes(1) });
        await db.SaveChangesAsync();
        Assert.Equal("BOOKING_CAPACITY_RESERVED", (await Assert.ThrowsAsync<ParkingOperationException>(() => service.Status(1, new() { PhysicalStatus = "RESERVED" }, 1, "ADMIN", null, default))).Code);
        Assert.Equal("BOOKING_CAPACITY_RESERVED", (await Assert.ThrowsAsync<ParkingOperationException>(() => service.Delete(1, 1, default))).Code);
        (await db.Bookings.FindAsync(1L))!.Status = "CANCELLED"; await db.SaveChangesAsync();
        Assert.True((await service.Status(1, new() { PhysicalStatus = "RESERVED" }, 1, "ADMIN", null, default)).Success);
        Assert.Equal(0, (await ParkingLotCapacityHelper.CalculateAsync(db, 1)).AvailableCapacity);
    }
}
