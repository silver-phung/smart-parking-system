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
        var capacity = await bookingService.GetLotCapacityAsync(1, currentUserId: 1, currentUserRole: Roles.Admin);
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
        var cap1 = await bookingService.GetLotCapacityAsync(8, currentUserId: 1, currentUserRole: Roles.Admin);
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
        var cap2 = await bookingService.GetLotCapacityAsync(8, currentUserId: 1, currentUserRole: Roles.Admin);
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
        var capBefore = await bookingService.GetLotCapacityAsync(11, currentUserId: 1, currentUserRole: Roles.Admin);
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
        var capAfter = await bookingService.GetLotCapacityAsync(11, currentUserId: 1, currentUserRole: Roles.Admin);
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
        var cap1 = await bookingService.GetLotCapacityAsync(13, currentUserId: 1, currentUserRole: Roles.Admin);
        Assert.NotNull(cap1);
        Assert.Equal(1, cap1.ActiveSessionsCount);
        Assert.Equal(0, cap1.AvailableForBooking);

        // Vehicle checks out
        var (checkOutSuccess, _, _) = await sessionService.CheckOutAsync(sessionDto!.Id, new CheckOutRequestDto(), 1, Roles.Admin, "127.0.0.1");
        Assert.True(checkOutSuccess);

        // Capacity is released
        var cap2 = await bookingService.GetLotCapacityAsync(13, currentUserId: 1, currentUserRole: Roles.Admin);
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

        var capacity = await bookingService.GetLotCapacityAsync(15, currentUserId: 1, currentUserRole: Roles.Admin);
        Assert.NotNull(capacity);
        Assert.Equal(4, capacity.TotalCapacity);
        Assert.Equal(0, capacity.ActiveSessionsCount);
        Assert.Equal(2, capacity.PhysicalOccupiedSlotsCount);
        Assert.NotNull(capacity.DiscrepancyNotes);
        Assert.Contains("Ghi nhận chênh lệch IoT: 2 ô vật lý báo có xe", capacity.DiscrepancyNotes);

        // Under M07-2 Conservative IoT Guard:
        // Used = max(ActiveSessions: 0, PhysicalOccupied: 2) + Holds: 0 = 2
        // Available = max(0, 4 - 2) = 2
        Assert.Equal(2, capacity.AvailableForBooking);
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

            var admin = new User { Id = 1, Username = "admin", Email = "admin@test.com", FullName = "Admin", PasswordHash = "x", RoleId = 1, IsActive = true, ApprovalStatus = "APPROVED" };
            var u1 = new User { Id = 10, Username = "user1", Email = "u1@test.com", FullName = "U1", PasswordHash = "x", RoleId = 4, IsActive = true, ApprovalStatus = "APPROVED" };
            var u2 = new User { Id = 20, Username = "user2", Email = "u2@test.com", FullName = "U2", PasswordHash = "x", RoleId = 4, IsActive = true, ApprovalStatus = "APPROVED" };
            seedContext.Users.AddRange(admin, u1, u2);

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
            var capacity = await verifyService.GetLotCapacityAsync(16, currentUserId: 1, currentUserRole: Roles.Admin);
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

    #region Task 07 Milestone Review Fixes (M07-1 to M07-4)

    [Fact]
    public async Task M07_1_CapacityFallback_NoConfiguredSlots_FallsBackToTotalCapacity()
    {
        var (bookingService, _, _, context) = await CreateServicesAsync();
        // Seed Lot with NO ParkingFloor or ParkingSlot records configured
        var lot = new ParkingLot
        {
            Id = 101,
            Name = "Bãi Chưa Cấu Hình Ô",
            Address = "101 Fallback St",
            TotalCapacity = 20,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.ParkingLots.Add(lot);

        // Seed 2 active sessions and 1 pending booking
        var vehicle = new Vehicle { Id = 10101, UserId = 10, PlateNumber = "51A-10101", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.Vehicles.Add(vehicle);
        context.ParkingSessions.Add(new ParkingSession
        {
            Id = 1011,
            LotId = 101,
            PlateNumber = "51A-10111",
            Status = SessionStatuses.Active,
            CheckInTime = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.ParkingSessions.Add(new ParkingSession
        {
            Id = 1012,
            LotId = 101,
            PlateNumber = "51A-10122",
            Status = SessionStatuses.Paid,
            CheckInTime = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        context.Bookings.Add(new Booking
        {
            Id = 1013,
            LotId = 101,
            UserId = 10,
            VehicleId = 10101,
            PlateNumber = "51A-10101",
            Status = BookingStatuses.Pending,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(2),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var capacity = await bookingService.GetLotCapacityAsync(101, currentUserId: 1, currentUserRole: Roles.Admin);
        Assert.NotNull(capacity);
        // Fallback permitted when NO slots exist
        Assert.Equal(20, capacity.TotalCapacity);
        Assert.Equal(2, capacity.ActiveSessionsCount);
        Assert.Equal(1, capacity.ActiveReservationsCount);
        // Available = 20 - (2 + 1) = 17
        Assert.Equal(17, capacity.AvailableForBooking);
        Assert.Equal(17, capacity.AvailableForWalkIn);
    }

    [Fact]
    public async Task M07_1_CapacityFallback_ConfiguredSlotsAllMaintenanceOrInactive_CapacityIsZeroNotFallback()
    {
        var (bookingService, sessionService, _, context) = await CreateServicesAsync();
        var lot = new ParkingLot
        {
            Id = 102,
            Name = "Bãi Hỏng Toàn Bộ Ô",
            Address = "102 Zero St",
            TotalCapacity = 50, // configured lot.TotalCapacity is 50, but all slots are unavailable
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.ParkingLots.Add(lot);

        var floor = new ParkingFloor
        {
            Id = 1020,
            LotId = 102,
            FloorNumber = 1,
            Name = "Tầng Hỏng",
            TotalSlots = 3,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.ParkingFloors.Add(floor);

        // Slot 1: Maintenance
        // Slot 2: Unavailable
        // Slot 3: Inactive
        context.ParkingSlots.AddRange(
            new ParkingSlot { Id = 1021, FloorId = 1020, SlotCode = "M-1", PhysicalStatus = SlotPhysicalStatuses.Maintenance, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new ParkingSlot { Id = 1022, FloorId = 1020, SlotCode = "U-2", PhysicalStatus = SlotPhysicalStatuses.Unavailable, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new ParkingSlot { Id = 1023, FloorId = 1020, SlotCode = "I-3", PhysicalStatus = SlotPhysicalStatuses.Empty, IsActive = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        );
        var v = new Vehicle { Id = 10201, UserId = 10, PlateNumber = "51A-10201", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.Vehicles.Add(v);
        await context.SaveChangesAsync();

        var capacity = await bookingService.GetLotCapacityAsync(102, currentUserId: 1, currentUserRole: Roles.Admin);
        Assert.NotNull(capacity);
        // CRITICAL INVARIANT: Usable capacity must be ZERO, NOT 50!
        Assert.Equal(0, capacity.TotalCapacity);
        Assert.Equal(0, capacity.AvailableForBooking);
        Assert.Equal(0, capacity.AvailableForWalkIn);
        Assert.True(capacity.IsFull);

        // Booking creation must be rejected with 409 Conflict
        var bookRes = await bookingService.CreateBookingAsync(new CreateBookingRequestDto { LotId = 102, VehicleId = 10201 }, 10, Roles.User, "127.0.0.1");
        Assert.False(bookRes.Success);
        Assert.Equal(409, bookRes.StatusCode);
        Assert.Contains("hết chỗ", bookRes.Message);

        // Walk-in check-in must be rejected
        var (checkInOk, checkInMsg, _) = await sessionService.CheckInAsync(new CheckInRequestDto { LotId = 102, PlateNumber = "51A-WALKIN102" }, 1, Roles.Admin, "127.0.0.1");
        Assert.False(checkInOk);
        Assert.Contains("hết chỗ", checkInMsg);
    }

    [Fact]
    public async Task M07_1_CapacityFallback_MixedActiveAndInactiveSlotsAndFloors_OnlyCountsUsableSlots()
    {
        var (bookingService, _, _, context) = await CreateServicesAsync();
        var lot = new ParkingLot
        {
            Id = 103,
            Name = "Bãi Hỗn Hợp Tầng",
            Address = "103 Mixed St",
            TotalCapacity = 100,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.ParkingLots.Add(lot);

        // Active Floor 1
        var floor1 = new ParkingFloor
        {
            Id = 1031,
            LotId = 103,
            FloorNumber = 1,
            Name = "Tầng 1 Hoạt Động",
            TotalSlots = 4,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        // Inactive Floor 2 (e.g. undergoing renovation)
        var floor2 = new ParkingFloor
        {
            Id = 1032,
            LotId = 103,
            FloorNumber = 2,
            Name = "Tầng 2 Đóng Cửa",
            TotalSlots = 5,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.ParkingFloors.AddRange(floor1, floor2);

        // Floor 1 slots: 2 active empty, 1 maintenance, 1 inactive
        context.ParkingSlots.AddRange(
            new ParkingSlot { Id = 10311, FloorId = 1031, SlotCode = "F1-01", PhysicalStatus = SlotPhysicalStatuses.Empty, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new ParkingSlot { Id = 10312, FloorId = 1031, SlotCode = "F1-02", PhysicalStatus = SlotPhysicalStatuses.Empty, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new ParkingSlot { Id = 10313, FloorId = 1031, SlotCode = "F1-03", PhysicalStatus = SlotPhysicalStatuses.Maintenance, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new ParkingSlot { Id = 10314, FloorId = 1031, SlotCode = "F1-04", PhysicalStatus = SlotPhysicalStatuses.Empty, IsActive = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        );

        // Floor 2 slots: all marked active empty, but whole floor is inactive
        for (int i = 1; i <= 5; i++)
        {
            context.ParkingSlots.Add(new ParkingSlot
            {
                Id = 10320 + i,
                FloorId = 1032,
                SlotCode = $"F2-0{i}",
                PhysicalStatus = SlotPhysicalStatuses.Empty,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await context.SaveChangesAsync();

        var capacity = await bookingService.GetLotCapacityAsync(103, currentUserId: 1, currentUserRole: Roles.Admin);
        Assert.NotNull(capacity);
        // Only 2 slots on Floor 1 are eligible & usable!
        Assert.Equal(2, capacity.TotalCapacity);
        Assert.Equal(2, capacity.AvailableForBooking);
    }

    [Fact]
    public async Task M07_1_Capacity_WhenReducedBelowCommittedSessions_RejectsNewAdmissionsAndPreservesHistorical()
    {
        var (bookingService, sessionService, _, context) = await CreateServicesAsync();
        var (_, _, slots) = SeedLotWithSlots(context, 104, "Bãi Cắt Giảm Chỗ Đột Ngột", slotCount: 2, activeSlotsCount: 2);

        var v1 = new Vehicle { Id = 10401, UserId = 10, PlateNumber = "51A-COMMITTED-1", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var v2 = new Vehicle { Id = 10402, UserId = 20, PlateNumber = "51A-COMMITTED-2", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.Vehicles.AddRange(v1, v2);
        await context.SaveChangesAsync();

        // 1 pending booking hold + 1 active walk-in session
        var bookRes = await bookingService.CreateBookingAsync(new CreateBookingRequestDto { LotId = 104, VehicleId = 10401 }, 10, Roles.User, "127.0.0.1");
        Assert.True(bookRes.Success);

        var (checkInOk, _, sessionDto) = await sessionService.CheckInAsync(new CheckInRequestDto { LotId = 104, PlateNumber = "51A-COMMITTED-2" }, 1, Roles.Admin, "127.0.0.1");
        Assert.True(checkInOk);

        // Now both physical slots are unexpectedly closed / marked maintenance
        foreach (var s in slots)
        {
            s.PhysicalStatus = SlotPhysicalStatuses.Maintenance;
        }
        await context.SaveChangesAsync();

        // Verify capacity is 0 (overcommitted)
        var capacity = await bookingService.GetLotCapacityAsync(104, currentUserId: 1, currentUserRole: Roles.Admin);
        Assert.NotNull(capacity);
        Assert.Equal(0, capacity.TotalCapacity);
        Assert.Equal(0, capacity.AvailableForBooking);
        Assert.Equal(0, capacity.AvailableForWalkIn);

        // Historical records are PRESERVED (not silently deleted or cancelled)
        var bookingInDb = await context.Bookings.FindAsync(bookRes.Data!.Id);
        Assert.NotNull(bookingInDb);
        Assert.Equal(BookingStatuses.Pending, bookingInDb.Status);

        var sessionInDb = await context.ParkingSessions.FindAsync(sessionDto!.Id);
        Assert.NotNull(sessionInDb);
        Assert.Equal(SessionStatuses.Active, sessionInDb.Status);

        // New admissions are strictly rejected
        var newWalkIn = await sessionService.CheckInAsync(new CheckInRequestDto { LotId = 104, PlateNumber = "51A-NEW-ATTEMPT" }, 1, Roles.Admin, "127.0.0.1");
        Assert.False(newWalkIn.Success);

        var newBook = await bookingService.CreateBookingAsync(new CreateBookingRequestDto { LotId = 104, VehicleId = 10401 }, 10, Roles.User, "127.0.0.1");
        Assert.False(newBook.Success);
        Assert.Equal(409, newBook.StatusCode);
    }

    [Fact]
    public async Task M07_2_ConservativeIoTGuard_OccupiedSensorsGreaterThanSessions()
    {
        var (bookingService, _, _, context) = await CreateServicesAsync();
        var (_, _, slots) = SeedLotWithSlots(context, 105, "Bãi IoT > Sessions", slotCount: 5, activeSlotsCount: 5);

        // 1 active session
        context.ParkingSessions.Add(new ParkingSession
        {
            Id = 1051,
            LotId = 105,
            PlateNumber = "51A-10511",
            Status = SessionStatuses.Active,
            CheckInTime = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // 3 physical slots reported OCCUPIED by sensors
        slots[0].PhysicalStatus = SlotPhysicalStatuses.Occupied;
        slots[1].PhysicalStatus = SlotPhysicalStatuses.Occupied;
        slots[2].PhysicalStatus = SlotPhysicalStatuses.Occupied;

        // 1 pending booking hold
        var v = new Vehicle { Id = 10501, UserId = 10, PlateNumber = "51A-10501", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.Vehicles.Add(v);
        context.Bookings.Add(new Booking
        {
            Id = 1052,
            LotId = 105,
            UserId = 10,
            VehicleId = 10501,
            PlateNumber = "51A-10501",
            Status = BookingStatuses.Pending,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(2),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        // Formula: Used = max(Sessions: 1, PhysicalOccupied: 3) + Holds: 1 = 4
        // Available = max(0, 5 - 4) = 1
        var cap = await bookingService.GetLotCapacityAsync(105, currentUserId: 1, currentUserRole: Roles.Admin);
        Assert.NotNull(cap);
        Assert.Equal(5, cap.TotalCapacity);
        Assert.Equal(1, cap.ActiveSessionsCount);
        Assert.Equal(3, cap.PhysicalOccupiedSlotsCount);
        Assert.Equal(1, cap.ActiveReservationsCount);
        Assert.Equal(4, cap.TotalOccupiedOrReserved);
        Assert.Equal(1, cap.AvailableForBooking);
        Assert.Equal(1, cap.AvailableForWalkIn);
        Assert.NotNull(cap.DiscrepancyNotes);
        Assert.Contains("3 ô vật lý báo có xe", cap.DiscrepancyNotes);
    }

    [Fact]
    public async Task M07_2_ConservativeIoTGuard_SessionsGreaterThanOccupiedSensors()
    {
        var (bookingService, _, _, context) = await CreateServicesAsync();
        var (_, _, slots) = SeedLotWithSlots(context, 106, "Bãi Sessions > IoT", slotCount: 5, activeSlotsCount: 5);

        // 3 active sessions
        for (int i = 1; i <= 3; i++)
        {
            context.ParkingSessions.Add(new ParkingSession
            {
                Id = 1060 + i,
                LotId = 106,
                PlateNumber = $"51A-1060{i}",
                Status = SessionStatuses.Active,
                CheckInTime = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        // Only 1 sensor reports OCCUPIED (sensors delayed or unresponsive)
        slots[0].PhysicalStatus = SlotPhysicalStatuses.Occupied;
        await context.SaveChangesAsync();

        // Formula: Used = max(Sessions: 3, PhysicalOccupied: 1) + Holds: 0 = 3
        // Available = max(0, 5 - 3) = 2
        var cap = await bookingService.GetLotCapacityAsync(106, currentUserId: 1, currentUserRole: Roles.Admin);
        Assert.NotNull(cap);
        Assert.Equal(5, cap.TotalCapacity);
        Assert.Equal(3, cap.ActiveSessionsCount);
        Assert.Equal(1, cap.PhysicalOccupiedSlotsCount);
        Assert.Equal(3, cap.TotalOccupiedOrReserved);
        Assert.Equal(2, cap.AvailableForBooking);
        Assert.NotNull(cap.DiscrepancyNotes);
        Assert.Contains("1 ô vật lý báo có xe", cap.DiscrepancyNotes);
        Assert.Contains("3 phiên đỗ xe", cap.DiscrepancyNotes);
    }

    [Fact]
    public async Task M07_2_ConservativeIoTGuard_SessionsEqualOccupiedSensors_NoDiscrepancy()
    {
        var (bookingService, _, _, context) = await CreateServicesAsync();
        var (_, _, slots) = SeedLotWithSlots(context, 107, "Bãi Khớp IoT", slotCount: 5, activeSlotsCount: 5);

        // 2 active sessions & 2 physical occupied slots
        for (int i = 1; i <= 2; i++)
        {
            context.ParkingSessions.Add(new ParkingSession
            {
                Id = 1070 + i,
                LotId = 107,
                PlateNumber = $"51A-1070{i}",
                Status = SessionStatuses.Active,
                CheckInTime = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            slots[i - 1].PhysicalStatus = SlotPhysicalStatuses.Occupied;
        }
        await context.SaveChangesAsync();

        // Formula: Used = max(2, 2) + 0 = 2, Available = 5 - 2 = 3
        var cap = await bookingService.GetLotCapacityAsync(107, currentUserId: 1, currentUserRole: Roles.Admin);
        Assert.NotNull(cap);
        Assert.Equal(3, cap.AvailableForBooking);
        Assert.Null(cap.DiscrepancyNotes); // No discrepancy when counts match!
    }

    [Fact]
    public async Task M07_2_CheckIn_ReservedArrivalWithZeroUnreservedSpaces_SucceedsAtomically()
    {
        var (bookingService, sessionService, _, context) = await CreateServicesAsync();
        // Lot with capacity = 1
        SeedLotWithSlots(context, 108, "Bãi Duy Nhất 1 Chỗ", slotCount: 1, activeSlotsCount: 1);

        var v = new Vehicle { Id = 10801, UserId = 10, PlateNumber = "51A-RES-ONLY", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.Vehicles.Add(v);
        await context.SaveChangesAsync();

        // User reserves the only slot
        var bookRes = await bookingService.CreateBookingAsync(new CreateBookingRequestDto { LotId = 108, VehicleId = 10801 }, 10, Roles.User, "127.0.0.1");
        Assert.True(bookRes.Success);

        // Public available capacity is now 0
        var capBefore = await bookingService.GetLotCapacityAsync(108);
        Assert.Equal(0, capBefore!.AvailableForBooking);
        Assert.Equal(0, capBefore.AvailableForWalkIn);

        // Walk-in vehicle is rejected
        var walkInRes = await sessionService.CheckInAsync(new CheckInRequestDto { LotId = 108, PlateNumber = "51A-STRANGER" }, 1, Roles.Admin, "127.0.0.1");
        Assert.False(walkInRes.Success);
        Assert.Contains("hết chỗ", walkInRes.Message);

        // Reserved vehicle arrives: MUST succeed atomically by consuming its existing hold
        var reservedCheckIn = await sessionService.CheckInAsync(new CheckInRequestDto { LotId = 108, PlateNumber = "51A-RES-ONLY" }, 1, Roles.Admin, "127.0.0.1");
        Assert.True(reservedCheckIn.Success, reservedCheckIn.Message);
        Assert.NotNull(reservedCheckIn.Data);
        Assert.Equal(bookRes.Data!.Id, reservedCheckIn.Data.BookingId);

        // Booking status is CHECKED_IN
        var bookingAfter = await context.Bookings.FindAsync(bookRes.Data!.Id);
        Assert.Equal(BookingStatuses.CheckedIn, bookingAfter!.Status);

        // After check-in: 1 session, 0 holds, available remains 0
        var capAfter = await bookingService.GetLotCapacityAsync(108, currentUserId: 1, currentUserRole: Roles.Admin);
        Assert.Equal(1, capAfter!.ActiveSessionsCount);
        Assert.Equal(0, capAfter.ActiveReservationsCount);
        Assert.Equal(0, capAfter.AvailableForBooking);
    }

    [Fact]
    public async Task M07_3_Concurrency_CancelBooking_UnderLotLock_IdempotentAfterCancellation()
    {
        var (bookingService, _, _, context) = await CreateServicesAsync();
        SeedLotWithSlots(context, 109, "Bãi Khóa Hủy", slotCount: 2, activeSlotsCount: 2);

        var v = new Vehicle { Id = 10901, UserId = 10, PlateNumber = "51A-LOCK-CANCEL", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.Vehicles.Add(v);
        await context.SaveChangesAsync();

        var bookRes = await bookingService.CreateBookingAsync(new CreateBookingRequestDto { LotId = 109, VehicleId = 10901 }, 10, Roles.User, "127.0.0.1");
        Assert.True(bookRes.Success);

        // First cancel succeeds
        var cancelRes1 = await bookingService.CancelBookingAsync(bookRes.Data!.Id, new CancelBookingRequestDto { Reason = "Đổi lịch" }, 10, Roles.User, "127.0.0.1");
        Assert.True(cancelRes1.Success);
        Assert.Equal(200, cancelRes1.StatusCode);

        // Second cancel attempt under lock detects status != Pending and returns 400 Bad Request
        var cancelRes2 = await bookingService.CancelBookingAsync(bookRes.Data!.Id, new CancelBookingRequestDto { Reason = "Thử lại" }, 10, Roles.User, "127.0.0.1");
        Assert.False(cancelRes2.Success);
        Assert.Equal(400, cancelRes2.StatusCode);
        Assert.Contains("đã bị hủy", cancelRes2.Message);
    }

    [Fact]
    public async Task M07_4_PublicCapacity_HidesOperationalDiagnosticsAnonymously()
    {
        var (bookingService, _, _, context) = await CreateServicesAsync();
        var (_, _, slots) = SeedLotWithSlots(context, 110, "Bãi Ẩn Chẩn Đoán", slotCount: 3, activeSlotsCount: 3);
        slots[0].PhysicalStatus = SlotPhysicalStatuses.Occupied;
        await context.SaveChangesAsync();

        // 1. Anonymous / Public query (isStaffOrAdmin = false)
        var publicCap = await bookingService.GetLotCapacityAsync(110);
        Assert.NotNull(publicCap);

        // Operational fields MUST be null in the C# object
        Assert.Null(publicCap.ActiveSessionsCount);
        Assert.Null(publicCap.ActiveReservationsCount);
        Assert.Null(publicCap.TotalOccupiedOrReserved);
        Assert.Null(publicCap.PhysicalOccupiedSlotsCount);
        Assert.Null(publicCap.PhysicalEmptySlotsCount);
        Assert.Null(publicCap.DiscrepancyNotes);

        // Public fields are present
        Assert.Equal(110, publicCap.LotId);
        Assert.Equal("Bãi Ẩn Chẩn Đoán", publicCap.LotName);
        Assert.Equal(3, publicCap.TotalCapacity);
        Assert.Equal(2, publicCap.AvailableForBooking); // 3 - max(0, 1) = 2

        // System.Text.Json serialization MUST omit the null fields (WhenWritingNull)
        var jsonOptions = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
        var json = System.Text.Json.JsonSerializer.Serialize(publicCap, jsonOptions);
        Assert.DoesNotContain("activeSessionsCount", json);
        Assert.DoesNotContain("activeReservationsCount", json);
        Assert.DoesNotContain("totalOccupiedOrReserved", json);
        Assert.DoesNotContain("physicalOccupiedSlotsCount", json);
        Assert.DoesNotContain("physicalEmptySlotsCount", json);
        Assert.DoesNotContain("discrepancyNotes", json);
        Assert.Contains("availableForBooking", json);
        Assert.Contains("totalCapacity", json);
    }

    [Fact]
    public async Task M07_4_OperationalCapacity_ExposesDiagnosticsForStaffAndAdmin()
    {
        var (bookingService, _, _, context) = await CreateServicesAsync();
        var (_, _, slots) = SeedLotWithSlots(context, 111, "Bãi Hiện Chẩn Đoán", slotCount: 3, activeSlotsCount: 3);
        slots[0].PhysicalStatus = SlotPhysicalStatuses.Occupied;
        await context.SaveChangesAsync();

        // 2. Staff / Admin query (isStaffOrAdmin = true)
        var staffCap = await bookingService.GetLotCapacityAsync(111, currentUserId: 1, currentUserRole: Roles.Admin);
        Assert.NotNull(staffCap);

        // Operational fields MUST be populated
        Assert.NotNull(staffCap.ActiveSessionsCount);
        Assert.Equal(0, staffCap.ActiveSessionsCount);
        Assert.NotNull(staffCap.ActiveReservationsCount);
        Assert.Equal(0, staffCap.ActiveReservationsCount);
        Assert.NotNull(staffCap.PhysicalOccupiedSlotsCount);
        Assert.Equal(1, staffCap.PhysicalOccupiedSlotsCount);
        Assert.NotNull(staffCap.TotalOccupiedOrReserved);
        Assert.Equal(1, staffCap.TotalOccupiedOrReserved);
        Assert.NotNull(staffCap.DiscrepancyNotes);

        // JSON serialization includes the diagnostic fields
        var jsonOptions = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
        var json = System.Text.Json.JsonSerializer.Serialize(staffCap, jsonOptions);
        Assert.Contains("activeSessionsCount", json);
        Assert.Contains("physicalOccupiedSlotsCount", json);
        Assert.Contains("discrepancyNotes", json);
    }

    [Fact]
    public async Task CapacityDiagnostics_HiddenFromAnonymousUserAndUnassignedStaff()
    {
        var (bookingService, _, _, context) = await CreateServicesAsync();
        var (_, _, slotsA) = SeedLotWithSlots(context, 120, "Bãi Staff Được Gán", slotCount: 3, activeSlotsCount: 3);
        SeedLotWithSlots(context, 121, "Bãi Staff Không Gán", slotCount: 3, activeSlotsCount: 3);
        slotsA[0].PhysicalStatus = SlotPhysicalStatuses.Occupied;

        context.StaffLotAssignments.Add(new StaffLotAssignment
        {
            UserId = 3,
            LotId = 120,
            AssignedBy = 1,
            AssignedAt = DateTime.UtcNow,
            IsActive = true
        });
        context.StaffLotAssignments.Add(new StaffLotAssignment
        {
            UserId = 2,
            LotId = 121,
            AssignedBy = 1,
            AssignedAt = DateTime.UtcNow,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var anonymous = await bookingService.GetLotCapacityAsync(120);
        Assert.Null(anonymous!.ActiveSessionsCount);
        Assert.Null(anonymous.DiscrepancyNotes);

        var userView = await bookingService.GetLotCapacityAsync(120, currentUserId: 10, currentUserRole: Roles.User);
        Assert.Null(userView!.PhysicalOccupiedSlotsCount);
        Assert.Null(userView.DiscrepancyNotes);

        var assignedStaff = await bookingService.GetLotCapacityAsync(120, currentUserId: 3, currentUserRole: Roles.Staff);
        Assert.Equal(1, assignedStaff!.PhysicalOccupiedSlotsCount);
        Assert.NotNull(assignedStaff.DiscrepancyNotes);

        var unassignedStaff = await bookingService.GetLotCapacityAsync(121, currentUserId: 3, currentUserRole: Roles.Staff);
        Assert.Null(unassignedStaff!.PhysicalOccupiedSlotsCount);
        Assert.Null(unassignedStaff.DiscrepancyNotes);

        var assignedManager = await bookingService.GetLotCapacityAsync(121, currentUserId: 2, currentUserRole: Roles.Manager);
        Assert.NotNull(assignedManager!.ActiveSessionsCount);

        var unassignedManager = await bookingService.GetLotCapacityAsync(120, currentUserId: 2, currentUserRole: Roles.Manager);
        Assert.Null(unassignedManager!.ActiveSessionsCount);

        var adminView = await bookingService.GetLotCapacityAsync(121, currentUserId: 1, currentUserRole: Roles.Admin);
        Assert.NotNull(adminView!.ActiveSessionsCount);
        Assert.NotNull(adminView.PhysicalOccupiedSlotsCount);
    }

    [Fact]
    public async Task M07_3_Concurrency_CheckOutReleasesCapacity_AllowingSubsequentWalkInUnderLock()
    {
        var (bookingService, sessionService, _, context) = await CreateServicesAsync();
        // Lot with exactly 1 slot
        SeedLotWithSlots(context, 112, "Bãi Tuần Tự Khóa", slotCount: 1, activeSlotsCount: 1);

        // Vehicle 1 checks in
        var checkIn1 = await sessionService.CheckInAsync(new CheckInRequestDto { LotId = 112, PlateNumber = "51A-SEQ-01" }, 1, Roles.Admin, "127.0.0.1");
        Assert.True(checkIn1.Success);

        // Capacity is 0; Vehicle 2 cannot check in
        var checkIn2Fail = await sessionService.CheckInAsync(new CheckInRequestDto { LotId = 112, PlateNumber = "51A-SEQ-02" }, 1, Roles.Admin, "127.0.0.1");
        Assert.False(checkIn2Fail.Success);

        // Vehicle 1 checks out (under per-lot lock)
        var checkOut1 = await sessionService.CheckOutAsync(checkIn1.Data!.Id, new CheckOutRequestDto(), 1, Roles.Admin, "127.0.0.1");
        Assert.True(checkOut1.Success);

        // Capacity immediately visible as 1 available
        var cap = await bookingService.GetLotCapacityAsync(112);
        Assert.Equal(1, cap!.AvailableForWalkIn);

        // Vehicle 2 checks in successfully
        var checkIn2Success = await sessionService.CheckInAsync(new CheckInRequestDto { LotId = 112, PlateNumber = "51A-SEQ-02" }, 1, Roles.Admin, "127.0.0.1");
        Assert.True(checkIn2Success.Success);
    }

    #endregion
}
