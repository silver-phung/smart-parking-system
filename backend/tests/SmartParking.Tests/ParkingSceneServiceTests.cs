using System.Text.Json;
using SmartParking.Domain.Constants;
using SmartParking.Domain.Entities;
using SmartParking.Infrastructure.Services;
using Xunit;

namespace SmartParking.Tests;

public class ParkingSceneServiceTests
{
    private const long OwnerId = 100, OtherUserId = 101, AdminId = 1, ManagerId = 2, ScopedStaffId = 3, GateStaffId = 4, LotWideStaffId = 5;

    private static User NewUser(long id, int roleId, bool active = true) => new()
    {
        Id = id, Username = $"u{id}", Email = $"u{id}@test.com", FullName = $"User {id}",
        PasswordHash = "h", RoleId = roleId, IsActive = active
    };

    private static StaffLotAssignment Assign(int id, long userId, string duty, int? floorId = null) => new()
    {
        Id = id, UserId = userId, LotId = 1, FloorId = floorId, Duty = duty, AssignedBy = AdminId,
        AssignedAt = DateTime.UtcNow, IsActive = true
    };

    private static async Task<(ParkingSceneService Service, TestSmartParkingDbContext Db)> SetupAsync(bool lotActive = true)
    {
        var db = await TestDbHelper.CreateInMemoryDbContextAsync();
        var now = DateTime.UtcNow;
        db.Users.AddRange(NewUser(AdminId, 1), NewUser(ManagerId, 2), NewUser(ScopedStaffId, 3), NewUser(GateStaffId, 3),
            NewUser(LotWideStaffId, 3), NewUser(OwnerId, 4), NewUser(OtherUserId, 4), NewUser(200, 4, active: false));
        db.ParkingLots.Add(new ParkingLot { Id = 1, Name = "Bãi 1", Address = "Q1", TotalCapacity = 4, IsActive = lotActive, CreatedAt = now, UpdatedAt = now });
        db.ParkingFloors.AddRange(
            new ParkingFloor { Id = 1, LotId = 1, FloorNumber = 1, Name = "Tầng 1", TotalSlots = 2, IsActive = true, CreatedAt = now, UpdatedAt = now },
            new ParkingFloor { Id = 2, LotId = 1, FloorNumber = 2, Name = "Tầng 2", TotalSlots = 2, IsActive = true, CreatedAt = now, UpdatedAt = now });
        db.ParkingSlots.AddRange(
            new ParkingSlot { Id = 1, FloorId = 1, SlotCode = "F1-01", PhysicalStatus = SlotPhysicalStatuses.Empty, IsActive = true, CreatedAt = now, UpdatedAt = now },
            new ParkingSlot { Id = 2, FloorId = 2, SlotCode = "F2-01", PhysicalStatus = SlotPhysicalStatuses.Empty, IsActive = true, CreatedAt = now, UpdatedAt = now });
        db.Vehicles.Add(new Vehicle { Id = 1, PlateNumber = "51A-11111", UserId = OwnerId, IsActive = true, CreatedAt = now, UpdatedAt = now });
        db.Vehicles.Add(new Vehicle { Id = 2, PlateNumber = "51A-22222", UserId = OtherUserId, IsActive = true, CreatedAt = now, UpdatedAt = now });
        db.ParkingSessions.AddRange(
            new ParkingSession { Id = 1, LotId = 1, VehicleId = 1, PlateNumber = "51A-11111", CurrentSlotId = 1, Status = SessionStatuses.Active, CheckInTime = now, CreatedAt = now, UpdatedAt = now },
            new ParkingSession { Id = 2, LotId = 1, VehicleId = 2, PlateNumber = "51A-22222", CurrentSlotId = 2, Status = SessionStatuses.Active, CheckInTime = now, CreatedAt = now, UpdatedAt = now });
        db.StaffLotAssignments.AddRange(
            Assign(1, ManagerId, StaffDuties.Both),
            Assign(2, ScopedStaffId, StaffDuties.SlotVerifier, floorId: 1),
            Assign(3, GateStaffId, StaffDuties.GateIn),
            Assign(4, LotWideStaffId, StaffDuties.Both));
        await db.SaveChangesAsync();
        return (new ParkingSceneService(db, new ParkingAuthorizationService(db)), db);
    }

    private static async Task<Dictionary<string, (string? Plate, bool HasSession)>> ReadSlots(ParkingSceneService service, long? actor, string role = "")
    {
        var result = await service.Read(1, actor, role, CancellationToken.None);
        Assert.NotNull(result);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(result));
        var slots = new Dictionary<string, (string?, bool)>();
        foreach (var floor in doc.RootElement.GetProperty("Floors").EnumerateArray())
            foreach (var s in floor.GetProperty("Slots").EnumerateArray())
                slots[s.GetProperty("Code").GetString()!] = (
                    s.GetProperty("Plate").ValueKind == JsonValueKind.Null ? null : s.GetProperty("Plate").GetString(),
                    s.GetProperty("SessionId").ValueKind != JsonValueKind.Null);
        return slots;
    }

    [Fact]
    public async Task Guest_SeesOccupancyButNoPlateOrSession()
    {
        var (service, _) = await SetupAsync();
        var slots = await ReadSlots(service, null);
        Assert.All(slots.Values, v => { Assert.Null(v.Plate); Assert.False(v.HasSession); });
    }

    [Fact]
    public async Task Customer_SeesOnlyOwnPlate()
    {
        var (service, _) = await SetupAsync();
        var slots = await ReadSlots(service, OwnerId, Roles.User);
        Assert.Equal("51A-11111", slots["F1-01"].Plate);
        Assert.Null(slots["F2-01"].Plate);
    }

    [Fact]
    public async Task InactiveAccount_IsTreatedAsGuest()
    {
        var (service, _) = await SetupAsync();
        var slots = await ReadSlots(service, 200, Roles.Admin);
        Assert.All(slots.Values, v => Assert.Null(v.Plate));
    }

    [Fact]
    public async Task Admin_SeesAllPlates()
    {
        var (service, _) = await SetupAsync();
        var slots = await ReadSlots(service, AdminId, Roles.Admin);
        Assert.Equal("51A-11111", slots["F1-01"].Plate);
        Assert.Equal("51A-22222", slots["F2-01"].Plate);
    }

    [Fact]
    public async Task AssignedManager_SeesAllPlatesInLot()
    {
        var (service, _) = await SetupAsync();
        var slots = await ReadSlots(service, ManagerId, Roles.Manager);
        Assert.Equal("51A-11111", slots["F1-01"].Plate);
        Assert.Equal("51A-22222", slots["F2-01"].Plate);
    }

    [Fact]
    public async Task Staff_WithFloorScope_SeesPlatesOnlyOnThatFloor()
    {
        var (service, _) = await SetupAsync();
        var slots = await ReadSlots(service, ScopedStaffId, Roles.Staff);
        Assert.Equal("51A-11111", slots["F1-01"].Plate);
        Assert.Null(slots["F2-01"].Plate);
        Assert.False(slots["F2-01"].HasSession);
    }

    [Fact]
    public async Task Staff_WithGateOnlyDuty_SeesNoPlates()
    {
        var (service, _) = await SetupAsync();
        var slots = await ReadSlots(service, GateStaffId, Roles.Staff);
        Assert.All(slots.Values, v => { Assert.Null(v.Plate); Assert.False(v.HasSession); });
    }

    [Fact]
    public async Task Staff_WithLotWideVerifierDuty_SeesAllPlates()
    {
        var (service, _) = await SetupAsync();
        var slots = await ReadSlots(service, LotWideStaffId, Roles.Staff);
        Assert.Equal("51A-11111", slots["F1-01"].Plate);
        Assert.Equal("51A-22222", slots["F2-01"].Plate);
    }

    [Fact]
    public async Task Staff_WithFloorScope_CanVerifyOnlyAssignedFloor()
    {
        var (service, _) = await SetupAsync();
        var result = await service.Read(1, ScopedStaffId, Roles.Staff, CancellationToken.None);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(result));
        var flags = doc.RootElement.GetProperty("Floors").EnumerateArray()
            .ToDictionary(f => f.GetProperty("Id").GetInt32(), f => f.GetProperty("CanVerify").GetBoolean());
        Assert.True(flags[1]);
        Assert.False(flags[2]);
    }

    [Fact]
    public async Task ClosedLot_IsHiddenFromGuestAndCustomer_ButVisibleToAdmin()
    {
        var (service, _) = await SetupAsync(lotActive: false);
        Assert.Null(await service.Read(1, null, "", CancellationToken.None));
        Assert.Null(await service.Read(1, OwnerId, Roles.User, CancellationToken.None));
        Assert.NotNull(await service.Read(1, AdminId, Roles.Admin, CancellationToken.None));
    }

    [Fact]
    public async Task UnknownLot_ReturnsNull()
    {
        var (service, _) = await SetupAsync();
        Assert.Null(await service.Read(999, null, "", CancellationToken.None));
    }
}

