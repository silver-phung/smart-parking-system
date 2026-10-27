using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.Api.Controllers;
using SmartParking.Application.Common.Helpers;
using SmartParking.Application.DTOs.Bookings;
using SmartParking.Application.DTOs.Sessions;
using SmartParking.Domain.Constants;
using SmartParking.Domain.Entities;
using SmartParking.Infrastructure.Services;
using Xunit;

namespace SmartParking.Tests;

public class BookingAndCapacityTests
{
    private async Task<(BookingService BookingService, ParkingSessionService SessionService, ParkingAuthorizationService AuthService, TestSmartParkingDbContext Context)> CreateServicesAsync()
    {
        var context = await TestDbHelper.CreateInMemoryDbContextAsync();

        // Seed Admin, Manager, Staff, User
        var adminUser = new User
        {
            Id = 1,
            Username = "admin_booking",
            Email = "admin@booking.com",
            FullName = "Admin Booking",
            PasswordHash = "dummy",
            RoleId = 1,
            IsActive = true,
            ApprovalStatus = "APPROVED"
        };
        var managerUser = new User
        {
            Id = 2,
            Username = "manager_booking",
            Email = "manager@booking.com",
            FullName = "Manager Booking",
            PasswordHash = "dummy",
            RoleId = 2,
            IsActive = true,
            ApprovalStatus = "APPROVED"
        };
        var staffUser = new User
        {
            Id = 3,
            Username = "staff_booking",
            Email = "staff@booking.com",
            FullName = "Staff Booking",
            PasswordHash = "dummy",
            RoleId = 3,
            IsActive = true,
            ApprovalStatus = "APPROVED"
        };
        var normalUser1 = new User
        {
            Id = 10,
            Username = "user1",
            Email = "user1@test.com",
            FullName = "User One",
            PasswordHash = "dummy",
            RoleId = 4,
            IsActive = true,
            ApprovalStatus = "APPROVED"
        };
        var normalUser2 = new User
        {
            Id = 20,
            Username = "user2",
            Email = "user2@test.com",
            FullName = "User Two",
            PasswordHash = "dummy",
            RoleId = 4,
            IsActive = true,
            ApprovalStatus = "APPROVED"
        };

        context.Users.AddRange(adminUser, managerUser, staffUser, normalUser1, normalUser2);
        await context.SaveChangesAsync();

        var authService = new ParkingAuthorizationService(context);
        var bookingService = new BookingService(context, authService, NullLogger<BookingService>.Instance);
        var sessionService = new ParkingSessionService(context, authService, NullLogger<ParkingSessionService>.Instance);

        return (bookingService, sessionService, authService, context);
    }

    private static (ParkingLot Lot, ParkingFloor Floor, List<ParkingSlot> Slots) SeedLotWithSlots(
        TestSmartParkingDbContext context, int lotId, string lotName, int slotCount, int activeSlotsCount)
    {
        var lot = new ParkingLot
        {
            Id = lotId,
            Name = lotName,
            Address = "123 Test Street",
            TotalCapacity = slotCount,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.ParkingLots.Add(lot);

        var floor = new ParkingFloor
        {
            Id = lotId * 10 + 1,
            LotId = lotId,
            FloorNumber = 1,
            Name = "Tầng 1",
            TotalSlots = slotCount,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.ParkingFloors.Add(floor);

        var slots = new List<ParkingSlot>();
        for (int i = 1; i <= slotCount; i++)
        {
            var isMaintenance = i > activeSlotsCount;
            var slot = new ParkingSlot
            {
                Id = lotId * 100 + i,
                FloorId = floor.Id,
                SlotCode = $"S-{lotId}-{i:D2}",
                PhysicalStatus = isMaintenance ? SlotPhysicalStatuses.Maintenance : SlotPhysicalStatuses.Empty,
                IsActive = !isMaintenance,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            slots.Add(slot);
            context.ParkingSlots.Add(slot);
        }

        context.SaveChanges();
        return (lot, floor, slots);
    }

    #region Phase 3 & 4: Booking Creation & Capacity Validation

    [Fact]
    public async Task CreateBooking_ValidRequest_ReservesCapacityWithoutPreassigningSlot()
    {
        var (bookingService, _, _, context) = await CreateServicesAsync();
        SeedLotWithSlots(context, 1, "Bãi Đỗ Xe Quận 1", slotCount: 10, activeSlotsCount: 10);

        var vehicle = new Vehicle
        {
            Id = 100,
            UserId = 10,
            PlateNumber = "51A-12345",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        var request = new CreateBookingRequestDto
        {
            LotId = 1,
            VehicleId = 100,
            Notes = "Đặt chỗ cho sáng mai"
        };

        var (success, statusCode, message, data) = await bookingService.CreateBookingAsync(request, 10, Roles.User, "127.0.0.1");

        Assert.True(success);
        Assert.Equal(201, statusCode);
        Assert.NotNull(data);
        Assert.Equal("51A-12345", data.PlateNumber);
        Assert.Equal(BookingStatuses.Pending, data.Status);
        Assert.Equal(1, data.LotId);
        Assert.Equal("Bãi Đỗ Xe Quận 1", data.LotName);

        // Verify Rule 1: Booking must reserve capacity, NOT permanently lock a slot
        var bookingInDb = await context.Bookings.FindAsync(data.Id);
        Assert.NotNull(bookingInDb);
        Assert.Equal(BookingStatuses.Pending, bookingInDb.Status);

        // Verify capacity is reserved (1 hold out of 10)
        var capacity = await bookingService.GetLotCapacityAsync(1);
        Assert.NotNull(capacity);
        Assert.Equal(10, capacity.TotalCapacity);
        Assert.Equal(1, capacity.ActiveReservationsCount);
        Assert.Equal(0, capacity.ActiveSessionsCount);
        Assert.Equal(9, capacity.AvailableForBooking);
    }

    [Fact]
    public async Task CreateBooking_WhenLotAtFullCapacity_Returns409Conflict()
    {
        var (bookingService, _, _, context) = await CreateServicesAsync();
        SeedLotWithSlots(context, 2, "Bãi Đỗ Nhỏ", slotCount: 1, activeSlotsCount: 1);

        var v1 = new Vehicle { Id = 201, UserId = 10, PlateNumber = "51A-11111", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var v2 = new Vehicle { Id = 202, UserId = 20, PlateNumber = "51A-22222", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.Vehicles.AddRange(v1, v2);
        await context.SaveChangesAsync();

        // User 1 books the only slot
        var r1 = await bookingService.CreateBookingAsync(new CreateBookingRequestDto { LotId = 2, VehicleId = 201 }, 10, Roles.User, "127.0.0.1");
        Assert.True(r1.Success);

        // User 2 tries to book when lot is full
        var r2 = await bookingService.CreateBookingAsync(new CreateBookingRequestDto { LotId = 2, VehicleId = 202 }, 20, Roles.User, "127.0.0.1");
        Assert.False(r2.Success);
        Assert.Equal(409, r2.StatusCode);
        Assert.Contains("hết chỗ", r2.Message);
    }

    [Fact]
    public async Task CreateBooking_InactiveLot_Returns400BadRequest()
    {
        var (bookingService, _, _, context) = await CreateServicesAsync();
        var (lot, _, _) = SeedLotWithSlots(context, 3, "Bãi Đóng Cửa", 5, 5);
        lot.IsActive = false;
        await context.SaveChangesAsync();

        var vehicle = new Vehicle { Id = 301, UserId = 10, PlateNumber = "51A-33333", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        var res = await bookingService.CreateBookingAsync(new CreateBookingRequestDto { LotId = 3, VehicleId = 301 }, 10, Roles.User, "127.0.0.1");
        Assert.False(res.Success);
        Assert.Equal(400, res.StatusCode);
        Assert.Contains("tạm ngừng", res.Message);
    }

    [Fact]
    public async Task CreateBooking_VehicleOfAnotherUser_Returns403Forbidden()
    {
        var (bookingService, _, _, context) = await CreateServicesAsync();
        SeedLotWithSlots(context, 4, "Bãi Thường", 10, 10);

        var vehicle = new Vehicle { Id = 401, UserId = 20, PlateNumber = "51A-44444", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        // User 10 attempts to book vehicle owned by User 20
        var res = await bookingService.CreateBookingAsync(new CreateBookingRequestDto { LotId = 4, VehicleId = 401 }, 10, Roles.User, "127.0.0.1");
        Assert.False(res.Success);
        Assert.Equal(403, res.StatusCode);
        Assert.Contains("không có quyền", res.Message);
    }

    [Fact]
    public async Task CreateBooking_DuplicatePendingBooking_Returns409Conflict()
    {
        var (bookingService, _, _, context) = await CreateServicesAsync();
        SeedLotWithSlots(context, 5, "Bãi Quận 3", 10, 10);

        var vehicle = new Vehicle { Id = 501, UserId = 10, PlateNumber = "51A-55555", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        var r1 = await bookingService.CreateBookingAsync(new CreateBookingRequestDto { LotId = 5, VehicleId = 501 }, 10, Roles.User, "127.0.0.1");
        Assert.True(r1.Success);

        // Attempt second booking for the same vehicle
        var r2 = await bookingService.CreateBookingAsync(new CreateBookingRequestDto { LotId = 5, VehicleId = 501 }, 10, Roles.User, "127.0.0.1");
        Assert.False(r2.Success);
        Assert.Equal(409, r2.StatusCode);
        Assert.Contains("đã có một lượt đặt chỗ đang có hiệu lực", r2.Message);
    }

    [Fact]
    public async Task CreateBooking_VehicleWithActiveSession_Returns409Conflict()
    {
        var (bookingService, _, _, context) = await CreateServicesAsync();
        SeedLotWithSlots(context, 6, "Bãi Đang Đỗ", 10, 10);

        var vehicle = new Vehicle { Id = 601, UserId = 10, PlateNumber = "51A-66666", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.Vehicles.Add(vehicle);

        // Active session inside parking lot
        var session = new ParkingSession
        {
            Id = 6001,
            LotId = 6,
            VehicleId = 601,
            PlateNumber = "51A-66666",
            CheckInTime = DateTime.UtcNow,
            Status = SessionStatuses.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.ParkingSessions.Add(session);
        await context.SaveChangesAsync();

        var res = await bookingService.CreateBookingAsync(new CreateBookingRequestDto { LotId = 6, VehicleId = 601 }, 10, Roles.User, "127.0.0.1");
        Assert.False(res.Success);
        Assert.Equal(409, res.StatusCode);
        Assert.Contains("phiên đỗ xe chưa kết thúc", res.Message);
    }

    #endregion

    #region Phase 3: Booking Viewing & Cancellation

    [Fact]
    public async Task GetMyBookings_ReturnsOnlyCurrentUserBookings()
    {
        var (bookingService, _, _, context) = await CreateServicesAsync();
        SeedLotWithSlots(context, 7, "Bãi Test My", 10, 10);

        var v1 = new Vehicle { Id = 701, UserId = 10, PlateNumber = "51A-77701", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var v2 = new Vehicle { Id = 702, UserId = 20, PlateNumber = "51A-77702", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.Vehicles.AddRange(v1, v2);
        await context.SaveChangesAsync();

        await bookingService.CreateBookingAsync(new CreateBookingRequestDto { LotId = 7, VehicleId = 701 }, 10, Roles.User, "127.0.0.1");
        await bookingService.CreateBookingAsync(new CreateBookingRequestDto { LotId = 7, VehicleId = 702 }, 20, Roles.User, "127.0.0.1");

        var user1Bookings = await bookingService.GetMyBookingsAsync(10, new BookingFilterDto());
        Assert.Equal(1, user1Bookings.TotalCount);
        Assert.Equal("51A-77701", user1Bookings.Items[0].PlateNumber);

        var user2Bookings = await bookingService.GetMyBookingsAsync(20, new BookingFilterDto());
        Assert.Equal(1, user2Bookings.TotalCount);
        Assert.Equal("51A-77702", user2Bookings.Items[0].PlateNumber);
    }

    [Fact]
    public async Task CancelBooking_PendingBooking_ReleasesCapacityHold()
    {
        var (bookingService, _, _, context) = await CreateServicesAsync();
        SeedLotWithSlots(context, 8, "Bãi Hủy Đặt Chỗ", 5, 5);

        var vehicle = new Vehicle { Id = 801, UserId = 10, PlateNumber = "51A-88888", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        var createRes = await bookingService.CreateBookingAsync(new CreateBookingRequestDto { LotId = 8, VehicleId = 801 }, 10, Roles.User, "127.0.0.1");
        Assert.True(createRes.Success);

        // Check hold was active
        var cap1 = await bookingService.GetLotCapacityAsync(8);
        Assert.NotNull(cap1);
        Assert.Equal(1, cap1.ActiveReservationsCount);
        Assert.Equal(4, cap1.AvailableForBooking);

        // Cancel booking
        var cancelReq = new CancelBookingRequestDto { Reason = "Kế hoạch thay đổi" };
        var cancelRes = await bookingService.CancelBookingAsync(createRes.Data!.Id, cancelReq, 10, Roles.User, "127.0.0.1");

        Assert.True(cancelRes.Success);
        Assert.Equal(200, cancelRes.StatusCode);
        Assert.Equal(BookingStatuses.Cancelled, cancelRes.Data!.Status);
        Assert.Equal("Kế hoạch thay đổi", cancelRes.Data.CancellationReason);

        // Verify capacity is released
        var cap2 = await bookingService.GetLotCapacityAsync(8);
        Assert.NotNull(cap2);
        Assert.Equal(0, cap2.ActiveReservationsCount);
        Assert.Equal(5, cap2.AvailableForBooking);
    }

    [Fact]
    public async Task CancelBooking_AlreadyCheckedIn_Returns400BadRequest()
    {
        var (bookingService, _, _, context) = await CreateServicesAsync();
        SeedLotWithSlots(context, 9, "Bãi Đã Đỗ", 5, 5);

        var vehicle = new Vehicle { Id = 901, UserId = 10, PlateNumber = "51A-99999", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.Vehicles.Add(vehicle);

        var booking = new Booking
        {
            Id = 9001,
            LotId = 9,
            UserId = 10,
            VehicleId = 901,
            PlateNumber = "51A-99999",
            StartTime = DateTime.UtcNow,
            Status = BookingStatuses.CheckedIn,
            CheckedInAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var cancelRes = await bookingService.CancelBookingAsync(9001, new CancelBookingRequestDto(), 10, Roles.User, "127.0.0.1");
        Assert.False(cancelRes.Success);
        Assert.Equal(400, cancelRes.StatusCode);
        Assert.Contains("đã được sử dụng", cancelRes.Message);
    }

    [Fact]
    public async Task CancelBooking_OtherUser_Returns403Forbidden()
    {
        var (bookingService, _, _, context) = await CreateServicesAsync();
        SeedLotWithSlots(context, 10, "Bãi Khác", 5, 5);

        var vehicle = new Vehicle { Id = 1001, UserId = 20, PlateNumber = "51A-10101", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.Vehicles.Add(vehicle);

        var booking = new Booking
        {
            Id = 10001,
            LotId = 10,
            UserId = 20,
            VehicleId = 1001,
            PlateNumber = "51A-10101",
            StartTime = DateTime.UtcNow,
            Status = BookingStatuses.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        // User 10 tries to cancel User 20's booking
        var cancelRes = await bookingService.CancelBookingAsync(10001, new CancelBookingRequestDto(), 10, Roles.User, "127.0.0.1");
        Assert.False(cancelRes.Success);
        Assert.Equal(403, cancelRes.StatusCode);
        Assert.Contains("không có quyền", cancelRes.Message);
    }

    #endregion

    #region Phase 4 & 6: Check-In Integration & No Double-Counting

    [Fact]
    public async Task CheckIn_ReservedVehicle_ConvertsHoldToAdmittedSessionWithoutDoubleCounting()
    {
        var (bookingService, sessionService, _, context) = await CreateServicesAsync();
        // Lot capacity = 2
        SeedLotWithSlots(context, 11, "Bãi Double-Count Test", slotCount: 2, activeSlotsCount: 2);

        var vehicle = new Vehicle { Id = 1101, UserId = 10, PlateNumber = "51A-11223", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        // Step 1: User 10 creates Booking
        var bookRes = await bookingService.CreateBookingAsync(new CreateBookingRequestDto { LotId = 11, VehicleId = 1101 }, 10, Roles.User, "127.0.0.1");
        Assert.True(bookRes.Success);

        // Before Check-in:
        // Total = 2, Admitted = 0, Reserved = 1, Available = 1
        var capBefore = await bookingService.GetLotCapacityAsync(11);
        Assert.NotNull(capBefore);
        Assert.Equal(2, capBefore.TotalCapacity);
        Assert.Equal(0, capBefore.ActiveSessionsCount);
        Assert.Equal(1, capBefore.ActiveReservationsCount);
        Assert.Equal(1, capBefore.AvailableForBooking);

        // Step 2: Vehicle arrives and checks in via Staff/Camera OCR
        var checkInReq = new CheckInRequestDto
        {
            LotId = 11,
            PlateNumber = "51a-11223", // lower case test
            CheckInImageUrl = "https://cdn.example.com/checkin/img1.jpg"
        };

        var (checkInSuccess, checkInMessage, sessionDto) = await sessionService.CheckInAsync(checkInReq, 1, Roles.Admin, "127.0.0.1");
        Assert.True(checkInSuccess, checkInMessage);
        Assert.NotNull(sessionDto);
        Assert.Equal(bookRes.Data!.Id, sessionDto.BookingId);

        // Booking status is atomically converted to CHECKED_IN
        var bookingAfter = await context.Bookings.FindAsync(bookRes.Data!.Id);
        Assert.NotNull(bookingAfter);
        Assert.Equal(BookingStatuses.CheckedIn, bookingAfter.Status);
        Assert.NotNull(bookingAfter.CheckedInAt);

        // INVARIANT VERIFICATION:
        // Total = 2, Admitted = 1, Reserved = 0, Available = 1!
        // The reserved vehicle did NOT consume an additional unreserved space!
        var capAfter = await bookingService.GetLotCapacityAsync(11);
        Assert.NotNull(capAfter);
        Assert.Equal(2, capAfter.TotalCapacity);
        Assert.Equal(1, capAfter.ActiveSessionsCount);
        Assert.Equal(0, capAfter.ActiveReservationsCount);
        Assert.Equal(1, capAfter.AvailableForBooking);
    }

    [Fact]
    public async Task CheckIn_WalkIn_RejectedWhenRemainingCapacityIsReserved()
    {
        var (bookingService, sessionService, _, context) = await CreateServicesAsync();
        // Lot capacity = 1
        SeedLotWithSlots(context, 12, "Bãi 1 Chỗ", slotCount: 1, activeSlotsCount: 1);

        var reservedVehicle = new Vehicle { Id = 1201, UserId = 10, PlateNumber = "51A-RESERVE", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.Vehicles.Add(reservedVehicle);
        await context.SaveChangesAsync();

        // 1 slot reserved by User 10
        var bookRes = await bookingService.CreateBookingAsync(new CreateBookingRequestDto { LotId = 12, VehicleId = 1201 }, 10, Roles.User, "127.0.0.1");
        Assert.True(bookRes.Success);

        // Available capacity is 0
        var cap = await bookingService.GetLotCapacityAsync(12);
        Assert.Equal(0, cap!.AvailableForBooking);

        // Walk-in vehicle attempts to check in
        var walkInReq = new CheckInRequestDto
        {
            LotId = 12,
            PlateNumber = "51A-WALKIN"
        };

        var (success, message, session) = await sessionService.CheckInAsync(walkInReq, 1, Roles.Admin, "127.0.0.1");

        // MUST be rejected! Walk-in cannot consume capacity reserved for another customer!
        Assert.False(success);
        Assert.Null(session);
        Assert.Contains("hết chỗ khả dụng cho khách vãng lai", message);
    }

    [Fact]
    public async Task CheckOut_ReleasesAdmittedCapacitySuccessfully()
    {
        var (bookingService, sessionService, _, context) = await CreateServicesAsync();
        SeedLotWithSlots(context, 13, "Bãi Check-Out Release", slotCount: 1, activeSlotsCount: 1);

        var walkInReq = new CheckInRequestDto { LotId = 13, PlateNumber = "51A-CHECKOUT" };
        var (checkInSuccess, _, sessionDto) = await sessionService.CheckInAsync(walkInReq, 1, Roles.Admin, "127.0.0.1");
        Assert.True(checkInSuccess);

        // Capacity is full
        var cap1 = await bookingService.GetLotCapacityAsync(13);
        Assert.NotNull(cap1);
        Assert.Equal(1, cap1.ActiveSessionsCount);
        Assert.Equal(0, cap1.AvailableForBooking);

        // Vehicle checks out
        var (checkOutSuccess, _, _) = await sessionService.CheckOutAsync(sessionDto!.Id, new CheckOutRequestDto(), 1, Roles.Admin, "127.0.0.1");
        Assert.True(checkOutSuccess);

        // Capacity is released
        var cap2 = await bookingService.GetLotCapacityAsync(13);
        Assert.NotNull(cap2);
        Assert.Equal(0, cap2.ActiveSessionsCount);
        Assert.Equal(1, cap2.AvailableForBooking);
    }

    [Fact]
    public async Task Capacity_ExcludesMaintenanceAndUnavailableSlots()
    {
        var (bookingService, _, _, context) = await CreateServicesAsync();
        // 5 total slots, but only 3 are active (2 are maintenance)
        SeedLotWithSlots(context, 14, "Bãi Bảo Trì", slotCount: 5, activeSlotsCount: 3);

        var capacity = await bookingService.GetLotCapacityAsync(14);
        Assert.NotNull(capacity);
        Assert.Equal(3, capacity.TotalCapacity);
        Assert.Equal(3, capacity.AvailableForBooking);
    }

    [Fact]
    public async Task Capacity_DetectsIoTOccupancyDiscrepancySafely()
    {
        var (bookingService, _, _, context) = await CreateServicesAsync();
        var (_, _, slots) = SeedLotWithSlots(context, 15, "Bãi Chênh Lệch IoT", slotCount: 4, activeSlotsCount: 4);

        // Simulate IoT sensor reporting 2 slots occupied, but no active sessions in backend
        slots[0].PhysicalStatus = SlotPhysicalStatuses.Occupied;
        slots[1].PhysicalStatus = SlotPhysicalStatuses.Occupied;
        await context.SaveChangesAsync();

        var capacity = await bookingService.GetLotCapacityAsync(15);
        Assert.NotNull(capacity);
        Assert.Equal(4, capacity.TotalCapacity);
        Assert.Equal(0, capacity.ActiveSessionsCount);
        Assert.Equal(2, capacity.PhysicalOccupiedSlotsCount);
        Assert.NotNull(capacity.DiscrepancyNotes);
        Assert.Contains("Ghi nhận chênh lệch IoT: 2 ô vật lý báo có xe", capacity.DiscrepancyNotes);

        // Discrepancy does not unsafely distort authoritative total capacity
        Assert.Equal(4, capacity.AvailableForBooking);
    }

    #endregion

    #region Phase 5: Concurrency Protection

    [Fact]
    public async Task ConcurrentBooking_LastAvailableCapacity_OnlyOneSucceeds()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<SmartParking.Infrastructure.Data.SmartParkingDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        // Seed with dedicated seedContext
        await using (var seedContext = new TestSmartParkingDbContext(options))
        {
            seedContext.Roles.AddRange(
                new Role { Id = 1, Code = Roles.Admin, Name = "Admin" },
                new Role { Id = 4, Code = Roles.User, Name = "User" });

            var u1 = new User { Id = 10, Username = "user1", Email = "u1@test.com", FullName = "U1", PasswordHash = "x", RoleId = 4, IsActive = true, ApprovalStatus = "APPROVED" };
            var u2 = new User { Id = 20, Username = "user2", Email = "u2@test.com", FullName = "U2", PasswordHash = "x", RoleId = 4, IsActive = true, ApprovalStatus = "APPROVED" };
            seedContext.Users.AddRange(u1, u2);

            SeedLotWithSlots(seedContext, 16, "Bãi Đua Chỗ", slotCount: 1, activeSlotsCount: 1);

            var v1 = new Vehicle { Id = 1601, UserId = 10, PlateNumber = "51A-RACE01", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            var v2 = new Vehicle { Id = 1602, UserId = 20, PlateNumber = "51A-RACE02", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            seedContext.Vehicles.AddRange(v1, v2);
            await seedContext.SaveChangesAsync();
        }

        // Each concurrent request has its own DbContext and BookingService instance sharing the same in-memory DB:
        var context1 = new TestSmartParkingDbContext(options);
        var authService1 = new ParkingAuthorizationService(context1);
        var bookingService1 = new BookingService(context1, authService1, NullLogger<BookingService>.Instance);

        var context2 = new TestSmartParkingDbContext(options);
        var authService2 = new ParkingAuthorizationService(context2);
        var bookingService2 = new BookingService(context2, authService2, NullLogger<BookingService>.Instance);

        var task1 = Task.Run(() => bookingService1.CreateBookingAsync(new CreateBookingRequestDto { LotId = 16, VehicleId = 1601 }, 10, Roles.User, "127.0.0.1"));
        var task2 = Task.Run(() => bookingService2.CreateBookingAsync(new CreateBookingRequestDto { LotId = 16, VehicleId = 1602 }, 20, Roles.User, "127.0.0.1"));

        var results = await Task.WhenAll(task1, task2);

        var successCount = results.Count(r => r.Success);
        var conflictCount = results.Count(r => !r.Success && r.StatusCode == 409);

        // Exactly 1 succeeds, and the other is rejected with 409 Conflict!
        Assert.Equal(1, successCount);
        Assert.Equal(1, conflictCount);

        await using (var verifyContext = new TestSmartParkingDbContext(options))
        {
            var verifyAuth = new ParkingAuthorizationService(verifyContext);
            var verifyService = new BookingService(verifyContext, verifyAuth, NullLogger<BookingService>.Instance);
            var capacity = await verifyService.GetLotCapacityAsync(16);
            Assert.NotNull(capacity);
            Assert.Equal(1, capacity.ActiveReservationsCount);
            Assert.Equal(0, capacity.AvailableForBooking);
        }
    }

    #endregion

    #region Phase 5 & Task 05: Staff Lot RBAC

    [Fact]
    public async Task Staff_CanOnlyViewBookingsForAssignedLot()
    {
        var (bookingService, _, _, context) = await CreateServicesAsync();
        SeedLotWithSlots(context, 17, "Bãi Lot 17", 5, 5);
        SeedLotWithSlots(context, 18, "Bãi Lot 18", 5, 5);

        // Assign staff (Id=3) to Lot 17 only
        context.StaffLotAssignments.Add(new StaffLotAssignment
        {
            UserId = 3,
            LotId = 17,
            AssignedAt = DateTime.UtcNow,
            AssignedBy = 1,
            IsActive = true
        });

        // Seed 1 booking in Lot 17 and 1 booking in Lot 18
        var v1 = new Vehicle { Id = 1701, UserId = 10, PlateNumber = "51A-LOT17", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var v2 = new Vehicle { Id = 1801, UserId = 20, PlateNumber = "51A-LOT18", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.Vehicles.AddRange(v1, v2);
        await context.SaveChangesAsync();

        await bookingService.CreateBookingAsync(new CreateBookingRequestDto { LotId = 17, VehicleId = 1701 }, 10, Roles.User, "127.0.0.1");
        await bookingService.CreateBookingAsync(new CreateBookingRequestDto { LotId = 18, VehicleId = 1801 }, 20, Roles.User, "127.0.0.1");

        // Staff listing without lot filter: should only return Lot 17
        var staffView = await bookingService.GetBookingsAsync(new BookingFilterDto(), 3, Roles.Staff);
        Assert.Equal(1, staffView.TotalCount);
        Assert.Equal(17, staffView.Items[0].LotId);

        // Staff trying to filter for Lot 18 (unauthorized): returns 0
        var staffViewUnauth = await bookingService.GetBookingsAsync(new BookingFilterDto { LotId = 18 }, 3, Roles.Staff);
        Assert.Equal(0, staffViewUnauth.TotalCount);

        // Admin view: returns both (TotalCount = 2)
        var adminView = await bookingService.GetBookingsAsync(new BookingFilterDto(), 1, Roles.Admin);
        Assert.Equal(2, adminView.TotalCount);
    }

    #endregion

    #region Controllers & Security Annotations

    [Fact]
    public void BookingsController_HasAuthorizeAttribute()
    {
        var authAttr = typeof(BookingsController).GetCustomAttribute<AuthorizeAttribute>();
        // Controller endpoints have appropriate Authorize annotations
        var createMethod = typeof(BookingsController).GetMethod(nameof(BookingsController.CreateBooking));
        Assert.NotNull(createMethod?.GetCustomAttribute<AuthorizeAttribute>());

        var getBookingsMethod = typeof(BookingsController).GetMethod(nameof(BookingsController.GetBookings));
        var staffAuthAttr = getBookingsMethod?.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(staffAuthAttr);
        Assert.Contains(Roles.Admin, staffAuthAttr.Roles!);
        Assert.Contains(Roles.Manager, staffAuthAttr.Roles!);
        Assert.Contains(Roles.Staff, staffAuthAttr.Roles!);
    }

    [Fact]
    public void ParkingLotsController_CapacityEndpoint_HasAllowAnonymousAttribute()
    {
        var method = typeof(ParkingLotsController).GetMethod(nameof(ParkingLotsController.GetLotCapacity));
        Assert.NotNull(method);
        var allowAnon = method.GetCustomAttribute<AllowAnonymousAttribute>();
        Assert.NotNull(allowAnon);
    }

    #endregion
}
