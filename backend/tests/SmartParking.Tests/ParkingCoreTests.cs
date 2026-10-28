using Microsoft.EntityFrameworkCore;
using SmartParking.Domain.Constants;
using SmartParking.Domain.Entities;
using Xunit;

namespace SmartParking.Tests;

public class ParkingCoreTests
{
    [Fact]
    public async Task DbContext_CanCreateAndQueryParkingCoreEntities()
    {
        // Arrange
        await using var context = await TestDbHelper.CreateInMemoryDbContextAsync();

        // 1. Create ParkingLot
        var lot = new ParkingLot
        {
            Name = "Bãi Đỗ Xe Trung Tâm FPT",
            Address = "Khu Công Nghệ Cao, TP. Thủ Đức",
            TotalCapacity = 100,
            IsActive = true
        };
        context.ParkingLots.Add(lot);
        await context.SaveChangesAsync();

        Assert.True(lot.Id > 0);

        // 2. Create ParkingFloor
        var floor = new ParkingFloor
        {
            LotId = lot.Id,
            FloorNumber = -1,
            Name = "Tầng Hầm B1",
            TotalSlots = 50,
            IsActive = true
        };
        context.ParkingFloors.Add(floor);
        await context.SaveChangesAsync();

        Assert.True(floor.Id > 0);

        // 3. Create ParkingSlot
        var slot = new ParkingSlot
        {
            FloorId = floor.Id,
            SlotCode = "B1-01",
            PhysicalStatus = SlotPhysicalStatuses.Empty,
            PositionX = 10.5m,
            PositionY = 20.0m,
            PositionZ = 0.0m,
            IsActive = true
        };
        context.ParkingSlots.Add(slot);
        await context.SaveChangesAsync();

        Assert.True(slot.Id > 0);

        // 4. Create Vehicle (Guest vehicle without User)
        var vehicle = new Vehicle
        {
            PlateNumber = "51H-12345",
            Brand = "Toyota",
            Model = "Camry",
            Color = "Black",
            UserId = null,
            IsActive = true
        };
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        Assert.True(vehicle.Id > 0);

        // 5. Create ParkingSession
        var session = new ParkingSession
        {
            LotId = lot.Id,
            VehicleId = vehicle.Id,
            PlateNumber = vehicle.PlateNumber,
            CheckInTime = DateTime.UtcNow,
            CurrentSlotId = null,
            Status = SessionStatuses.Active,
            TotalFee = 0m
        };
        context.ParkingSessions.Add(session);
        await context.SaveChangesAsync();

        Assert.True(session.Id > 0);

        // 6. Create ParkingDispatch
        var dispatch = new ParkingDispatch
        {
            SessionId = session.Id,
            AssignedSlotId = slot.Id,
            Status = DispatchStatuses.Assigned,
            DispatchMode = DispatchModes.Auto,
            DispatchedAt = DateTime.UtcNow
        };
        context.ParkingDispatches.Add(dispatch);
        await context.SaveChangesAsync();

        Assert.True(dispatch.Id > 0);

        // 7. Create AuditLog
        var audit = new AuditLog
        {
            ActorId = null,
            Action = "DISPATCH_ASSIGNED",
            TargetEntity = "parking_dispatches",
            TargetId = dispatch.Id.ToString(),
            NewValues = "{\"assigned_slot_id\":" + slot.Id + "}",
            IpAddress = "127.0.0.1"
        };
        context.AuditLogs.Add(audit);
        await context.SaveChangesAsync();

        Assert.True(audit.Id > 0);

        // Query back and verify navigation
        var savedSession = await context.ParkingSessions
            .Include(s => s.Lot)
            .Include(s => s.Vehicle)
            .Include(s => s.Dispatches)
                .ThenInclude(d => d.AssignedSlot)
            .FirstOrDefaultAsync(s => s.Id == session.Id);

        Assert.NotNull(savedSession);
        Assert.Equal("Bãi Đỗ Xe Trung Tâm FPT", savedSession.Lot.Name);
        Assert.Equal("51H-12345", savedSession.PlateNumber);
        Assert.Single(savedSession.Dispatches);
        Assert.Equal("B1-01", savedSession.Dispatches.First().AssignedSlot.SlotCode);
    }

    [Fact]
    public async Task ParkingSlot_DoesNotContainIsReservedProperty()
    {
        // Business Invariant: Slot physical status must be decoupled from dispatch assignment
        var slotType = typeof(ParkingSlot);
        var isReservedProp = slotType.GetProperty("IsReserved");
        var isReservedLowerProp = slotType.GetProperty("is_reserved");

        Assert.Null(isReservedProp);
        Assert.Null(isReservedLowerProp);
    }

    [Fact]
    public async Task ModelBuilder_ConfiguresAllSevenTablesWithCorrectNames()
    {
        await using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var model = context.Model;

        var lotEntity = model.FindEntityType(typeof(ParkingLot));
        var floorEntity = model.FindEntityType(typeof(ParkingFloor));
        var slotEntity = model.FindEntityType(typeof(ParkingSlot));
        var vehicleEntity = model.FindEntityType(typeof(Vehicle));
        var sessionEntity = model.FindEntityType(typeof(ParkingSession));
        var dispatchEntity = model.FindEntityType(typeof(ParkingDispatch));
        var auditEntity = model.FindEntityType(typeof(AuditLog));

        Assert.NotNull(lotEntity);
        Assert.Equal("parking_lots", lotEntity.GetTableName());

        Assert.NotNull(floorEntity);
        Assert.Equal("parking_floors", floorEntity.GetTableName());

        Assert.NotNull(slotEntity);
        Assert.Equal("parking_slots", slotEntity.GetTableName());

        Assert.NotNull(vehicleEntity);
        Assert.Equal("vehicles", vehicleEntity.GetTableName());

        Assert.NotNull(sessionEntity);
        Assert.Equal("parking_sessions", sessionEntity.GetTableName());

        Assert.NotNull(dispatchEntity);
        Assert.Equal("parking_dispatches", dispatchEntity.GetTableName());

        Assert.NotNull(auditEntity);
        Assert.Equal("audit_logs", auditEntity.GetTableName());
    }

    [Fact]
    public async Task ModelBuilder_AccountsTablesRemainIntact()
    {
        await using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var model = context.Model;

        var userEntity = model.FindEntityType(typeof(User));
        var roleEntity = model.FindEntityType(typeof(Role));
        var tokenEntity = model.FindEntityType(typeof(UserToken));
        var refreshEntity = model.FindEntityType(typeof(RefreshToken));

        Assert.NotNull(userEntity);
        Assert.Equal("users", userEntity.GetTableName());

        Assert.NotNull(roleEntity);
        Assert.Equal("roles", roleEntity.GetTableName());

        Assert.NotNull(tokenEntity);
        Assert.Equal("user_tokens", tokenEntity.GetTableName());

        Assert.NotNull(refreshEntity);
        Assert.Equal("refresh_tokens", refreshEntity.GetTableName());
    }

    [Fact]
    public async Task ParkingSession_StatusTransitions_FollowAllowedValues()
    {
        await using var context = await TestDbHelper.CreateInMemoryDbContextAsync();

        var lot = new ParkingLot { Name = "Test Lot", TotalCapacity = 10 };
        context.ParkingLots.Add(lot);
        await context.SaveChangesAsync();

        var session = new ParkingSession
        {
            LotId = lot.Id,
            PlateNumber = "51A-99999",
            Status = SessionStatuses.Active
        };
        context.ParkingSessions.Add(session);
        await context.SaveChangesAsync();

        // Active -> Paid
        session.Status = SessionStatuses.Paid;
        session.PaidAt = DateTime.UtcNow;
        session.TotalFee = 25000m;
        await context.SaveChangesAsync();

        var paidSession = await context.ParkingSessions.FindAsync(session.Id);
        Assert.NotNull(paidSession);
        Assert.Equal(SessionStatuses.Paid, paidSession.Status);
        Assert.Equal(25000m, paidSession.TotalFee);

        // Paid -> Completed
        session.Status = SessionStatuses.Completed;
        session.CheckOutTime = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var completedSession = await context.ParkingSessions.FindAsync(session.Id);
        Assert.NotNull(completedSession);
        Assert.Equal(SessionStatuses.Completed, completedSession.Status);
    }

    [Fact]
    public void PlateNumber_AutoNormalizes_TrimsAndConvertsToUppercase()
    {
        var vehicle = new Vehicle { PlateNumber = "  51h-123.45  " };
        Assert.Equal("51H-123.45", vehicle.PlateNumber);

        var session = new ParkingSession { PlateNumber = "  29a-99999  " };
        Assert.Equal("29A-99999", session.PlateNumber);
    }

    [Fact]
    public async Task ModelBuilder_ConfiguresAllForeignKeys_WithCorrectDeleteBehaviors()
    {
        await using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var model = context.Model;

        // Lot -> Floors: Cascade
        var floorEntity = model.FindEntityType(typeof(ParkingFloor))!;
        var lotFloorFk = floorEntity.GetForeignKeys().First(fk => fk.PrincipalEntityType.ClrType == typeof(ParkingLot));
        Assert.Equal(DeleteBehavior.Cascade, lotFloorFk.DeleteBehavior);

        // Floor -> Slots: Cascade
        var slotEntity = model.FindEntityType(typeof(ParkingSlot))!;
        var floorSlotFk = slotEntity.GetForeignKeys().First(fk => fk.PrincipalEntityType.ClrType == typeof(ParkingFloor));
        Assert.Equal(DeleteBehavior.Cascade, floorSlotFk.DeleteBehavior);

        // Session -> Lot, Vehicle, Slot: Restrict (History Protection)
        var sessionEntity = model.FindEntityType(typeof(ParkingSession))!;
        foreach (var fk in sessionEntity.GetForeignKeys())
        {
            Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
        }

        // Dispatch -> AssignedSlot, ActualSlot: Restrict
        var dispatchEntity = model.FindEntityType(typeof(ParkingDispatch))!;
        var assignedFk = dispatchEntity.GetForeignKeys().First(fk => fk.Properties.Any(p => p.Name == "AssignedSlotId"));
        Assert.Equal(DeleteBehavior.Restrict, assignedFk.DeleteBehavior);

        // Dispatch -> Session: Cascade (Dispatch is part of Session lifecycle)
        var sessionFk = dispatchEntity.GetForeignKeys().First(fk => fk.Properties.Any(p => p.Name == "SessionId"));
        Assert.Equal(DeleteBehavior.Cascade, sessionFk.DeleteBehavior);

        // User -> Vehicles: SetNull (Guest preservation)
        var vehicleEntity = model.FindEntityType(typeof(Vehicle))!;
        var userVehicleFk = vehicleEntity.GetForeignKeys().First(fk => fk.PrincipalEntityType.ClrType == typeof(User));
        Assert.Equal(DeleteBehavior.SetNull, userVehicleFk.DeleteBehavior);

        // User -> AuditLogs: SetNull (Audit preservation)
        var auditEntity = model.FindEntityType(typeof(AuditLog))!;
        var userAuditFk = auditEntity.GetForeignKeys().First(fk => fk.PrincipalEntityType.ClrType == typeof(User));
        Assert.Equal(DeleteBehavior.SetNull, userAuditFk.DeleteBehavior);
    }
}
