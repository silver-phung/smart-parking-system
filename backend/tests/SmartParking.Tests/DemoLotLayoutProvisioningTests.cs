using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.Api.Controllers;
using SmartParking.Application.DTOs.Bookings;
using SmartParking.Application.DTOs.Sessions;
using SmartParking.Domain.Constants;
using SmartParking.Domain.Entities;
using SmartParking.Infrastructure.Services;
using Xunit;

namespace SmartParking.Tests;

/// <summary>
/// Regression tests cho cấu hình bãi demo 300 ô (Tầng A/B/C x 100). InMemory PASS != PostgreSQL PASS:
/// cần chạy thủ công script SQL trên DB local/staging trước khi demo.
/// </summary>
public class DemoLotLayoutProvisioningTests
{
    private async Task<(ParkingManagementService Mgmt, BookingService Booking, ParkingSessionService Sessions, TestSmartParkingDbContext Context)> CreateAsync()
    {
        var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        context.Users.AddRange(
            new User { Id = 1, Username = "admin_demo", Email = "admin@demo.com", FullName = "Admin Demo", PasswordHash = "dummy", RoleId = 1, IsActive = true, ApprovalStatus = "APPROVED" },
            new User { Id = 10, Username = "user_demo", Email = "user@demo.com", FullName = "User Demo", PasswordHash = "dummy", RoleId = 4, IsActive = true, ApprovalStatus = "APPROVED" });
        await context.SaveChangesAsync();
        var auth = new ParkingAuthorizationService(context);
        return (new ParkingManagementService(context, NullLogger<ParkingManagementService>.Instance),
                new BookingService(context, auth, NullLogger<BookingService>.Instance),
                new ParkingSessionService(context, auth, NullLogger<ParkingSessionService>.Instance),
                context);
    }

    private static async Task<int> SeedEmptyLotAsync(TestSmartParkingDbContext context, int id = 300, int configured = 0, bool active = true)
    {
        context.ParkingLots.Add(new ParkingLot { Id = id, Name = $"Bãi Demo {id}", Address = "Demo", TotalCapacity = configured, IsActive = active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Provision_EmptyLot_Creates300SlotsAcrossFloorsABC()
    {
        var (mgmt, _, _, context) = await CreateAsync();
        var lotId = await SeedEmptyLotAsync(context);

        var (ok, _, result) = await mgmt.ProvisionStandardLayoutAsync(lotId, 1);

        Assert.True(ok);
        Assert.Equal(3, result!.FloorsCreated);
        Assert.Equal(300, result.SlotsCreated);
        Assert.Equal(300, result.PhysicalSlotsCount);
        Assert.Equal(300, result.ConfiguredCapacity);
        Assert.Equal(300, result.OperationalCapacity);

        var floors = await context.ParkingFloors.Where(f => f.LotId == lotId).OrderBy(f => f.FloorNumber).ToListAsync();
        Assert.Equal(new[] { "Tầng A", "Tầng B", "Tầng C" }, floors.Select(f => f.Name));
        Assert.All(floors, f => Assert.Equal(100, f.TotalSlots));

        var codes = (await context.ParkingSlots.Where(s => s.Floor.LotId == lotId).Select(s => s.SlotCode).ToListAsync()).ToHashSet();
        Assert.Equal(300, codes.Count);
        Assert.Contains("A001", codes); Assert.Contains("A100", codes);
        Assert.Contains("B001", codes); Assert.Contains("B100", codes);
        Assert.Contains("C001", codes); Assert.Contains("C100", codes);

        var dto = await mgmt.GetLotByIdAsync(lotId, true);
        Assert.Equal(300, dto!.TotalCapacity);
        Assert.Equal(300, dto.PhysicalSlotsCount);
        Assert.Equal(300, dto.ConfiguredCapacity);
        Assert.Equal(300, dto.AvailableSlotsCount);
        Assert.Equal(0, dto.OccupiedSlotsCount);
        Assert.NotNull(await context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "PROVISION_LOT_LAYOUT"));
    }

    [Fact]
    public async Task Provision_IsIdempotent_NoDuplicatesOnSecondRun()
    {
        var (mgmt, _, _, context) = await CreateAsync();
        var lotId = await SeedEmptyLotAsync(context);

        await mgmt.ProvisionStandardLayoutAsync(lotId, 1);
        var (ok, _, second) = await mgmt.ProvisionStandardLayoutAsync(lotId, 1);

        Assert.True(ok);
        Assert.Equal(0, second!.FloorsCreated);
        Assert.Equal(0, second.SlotsCreated);
        Assert.Equal(300, second.SlotsAlreadyExisting);
        Assert.Equal(300, await context.ParkingSlots.CountAsync(s => s.Floor.LotId == lotId));
        Assert.Equal(3, await context.ParkingFloors.CountAsync(f => f.LotId == lotId));
    }

    [Fact]
    public async Task Provision_PreservesExistingSlotStatusAndOnlyFillsGaps()
    {
        var (mgmt, _, _, context) = await CreateAsync();
        var lotId = await SeedEmptyLotAsync(context, configured: 100000);
        context.ParkingFloors.Add(new ParkingFloor { Id = 3001, LotId = lotId, FloorNumber = 1, Name = "Tầng A", TotalSlots = 1, IsActive = true });
        context.ParkingSlots.Add(new ParkingSlot { FloorId = 3001, SlotCode = "A005", PhysicalStatus = SlotPhysicalStatuses.Maintenance, IsActive = true });
        await context.SaveChangesAsync();

        var (ok, _, result) = await mgmt.ProvisionStandardLayoutAsync(lotId, 1);

        Assert.True(ok);
        Assert.Equal(2, result!.FloorsCreated);
        Assert.Equal(299, result.SlotsCreated);
        Assert.Equal(1, result.SlotsAlreadyExisting);
        Assert.Equal(300, result.PhysicalSlotsCount);
        Assert.Equal(300, result.ConfiguredCapacity); // 100000 legacy value normalised
        Assert.Equal(299, result.OperationalCapacity); // maintenance slot excluded
        var a005 = await context.ParkingSlots.SingleAsync(s => s.FloorId == 3001 && s.SlotCode == "A005");
        Assert.Equal(SlotPhysicalStatuses.Maintenance, a005.PhysicalStatus);
    }

    [Fact]
    public async Task Provision_UnknownLot_Fails()
    {
        var (mgmt, _, _, _) = await CreateAsync();
        var (ok, _, result) = await mgmt.ProvisionStandardLayoutAsync(9999, 1);
        Assert.False(ok);
        Assert.Null(result);
    }

    [Fact]
    public async Task Provisioned300Lot_BookingAndCheckInCountersStayConsistent()
    {
        var (mgmt, booking, sessions, context) = await CreateAsync();
        var lotId = await SeedEmptyLotAsync(context);
        await mgmt.ProvisionStandardLayoutAsync(lotId, 1);
        context.Vehicles.Add(new Vehicle { Id = 1, UserId = 10, PlateNumber = "51A-30000", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var cap0 = await booking.GetLotCapacityAsync(lotId, 1, Roles.Admin);
        Assert.Equal(300, cap0!.TotalCapacity);
        Assert.Equal(300, cap0.AvailableForBooking);

        var book = await booking.CreateBookingAsync(new CreateBookingRequestDto { LotId = lotId, VehicleId = 1 }, 10, Roles.User, "127.0.0.1");
        Assert.True(book.Success);
        var cap1 = await booking.GetLotCapacityAsync(lotId, 1, Roles.Admin);
        Assert.Equal(1, cap1!.ActiveReservationsCount);
        Assert.Equal(299, cap1.AvailableForBooking);

        var inReserved = await sessions.CheckInAsync(new CheckInRequestDto { LotId = lotId, PlateNumber = "51A-30000" }, 1, Roles.Admin, "127.0.0.1");
        Assert.True(inReserved.Success, inReserved.Message);
        var inWalk = await sessions.CheckInAsync(new CheckInRequestDto { LotId = lotId, PlateNumber = "51A-30001" }, 1, Roles.Admin, "127.0.0.1");
        Assert.True(inWalk.Success, inWalk.Message);

        var cap2 = await booking.GetLotCapacityAsync(lotId, 1, Roles.Admin);
        Assert.Equal(300, cap2!.TotalCapacity);
        Assert.Equal(2, cap2.ActiveSessionsCount);
        Assert.Equal(0, cap2.ActiveReservationsCount);
        Assert.Equal(298, cap2.AvailableForBooking);
    }

    [Fact]
    public void ProvisionEndpoint_IsAdminOnly()
    {
        var method = typeof(ParkingLotsController).GetMethod("ProvisionStandardLayout")!;
        var auth = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(auth);
        Assert.Equal(Roles.Admin, auth!.Roles);
    }

    [Fact]
    public async Task Provision_ConcurrentRequests_BothSucceedWithoutError()
    {
        var (mgmt, _, _, context) = await CreateAsync();
        var lotId = await SeedEmptyLotAsync(context);

        var task1 = Task.Run(() => mgmt.ProvisionStandardLayoutAsync(lotId, 1));
        var task2 = Task.Run(() => mgmt.ProvisionStandardLayoutAsync(lotId, 1));

        var results = await Task.WhenAll(task1, task2);

        Assert.True(results[0].Success);
        Assert.True(results[1].Success);
        Assert.Equal(300, await context.ParkingSlots.CountAsync(s => s.Floor.LotId == lotId));
        Assert.Equal(3, await context.ParkingFloors.CountAsync(f => f.LotId == lotId));
    }
}
