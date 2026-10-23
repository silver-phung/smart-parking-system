using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.Api.Controllers;
using SmartParking.Application.Common.Helpers;
using SmartParking.Application.DTOs.Sessions;
using SmartParking.Application.DTOs.Vehicles;
using SmartParking.Domain.Constants;
using SmartParking.Domain.Entities;
using SmartParking.Infrastructure.Services;
using Xunit;

namespace SmartParking.Tests;

public class VehicleAndSessionTests
{
    private async Task<(VehicleService VehicleService, ParkingSessionService SessionService, TestSmartParkingDbContext Context)> CreateServicesAsync()
    {
        var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var vehicleService = new VehicleService(context, NullLogger<VehicleService>.Instance);
        var authService = new ParkingAuthorizationService(context);
        var sessionService = new ParkingSessionService(context, authService, NullLogger<ParkingSessionService>.Instance);
        return (vehicleService, sessionService, context);
    }

    #region PlateNumberHelper Tests

    [Theory]
    [InlineData("51a-12345", "51A-12345")]
    [InlineData("  29b-999.99  ", "29B-999.99")]
    [InlineData("60c  12345", "60C 12345")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void PlateNumberHelper_Normalize_ReturnsCleanPlate(string? input, string expected)
    {
        var result = PlateNumberHelper.Normalize(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("51A-12345", true)]
    [InlineData("A1", false)] // too short
    [InlineData("123456789012345678901", false)] // too long
    public void PlateNumberHelper_IsValidPlateFormat_ValidatesCorrectly(string plate, bool expected)
    {
        var result = PlateNumberHelper.IsValidPlateFormat(plate);
        Assert.Equal(expected, result);
    }

    #endregion

    #region VehicleService Tests

    [Fact]
    public async Task CreateVehicle_UserOwnsVehicle_CreatesWithUserId()
    {
        var (vehicleService, _, context) = await CreateServicesAsync();

        var request = new CreateVehicleRequestDto
        {
            PlateNumber = " 51A-88888 ",
            Brand = "Toyota",
            Model = "Camry",
            Color = "Black"
        };

        var (success, message, vehicle) = await vehicleService.CreateVehicleAsync(request, currentUserId: 10, isAdmin: false);

        Assert.True(success);
        Assert.NotNull(vehicle);
        Assert.Equal("51A-88888", vehicle.PlateNumber);
        Assert.Equal(10, vehicle.UserId);
        Assert.Equal("Toyota", vehicle.Brand);

        var dbVehicle = context.Vehicles.FirstOrDefault(v => v.PlateNumber == "51A-88888");
        Assert.NotNull(dbVehicle);
        Assert.Equal(10, dbVehicle.UserId);
    }

    [Fact]
    public async Task CreateVehicle_DuplicatePlate_SameUser_ReturnsFailure()
    {
        var (vehicleService, _, context) = await CreateServicesAsync();
        context.Vehicles.Add(new Vehicle { PlateNumber = "51A-11111", UserId = 10, IsActive = true });
        await context.SaveChangesAsync();

        var request = new CreateVehicleRequestDto { PlateNumber = "51A-11111" };
        var (success, message, _) = await vehicleService.CreateVehicleAsync(request, currentUserId: 10, isAdmin: false);

        Assert.False(success);
        Assert.Contains("đã được đăng ký trong tài khoản của bạn", message);
    }

    [Fact]
    public async Task CreateVehicle_DuplicatePlate_OtherUser_ReturnsFailure()
    {
        var (vehicleService, _, context) = await CreateServicesAsync();
        context.Vehicles.Add(new Vehicle { PlateNumber = "51A-22222", UserId = 20, IsActive = true });
        await context.SaveChangesAsync();

        var request = new CreateVehicleRequestDto { PlateNumber = "51A-22222" };
        var (success, message, _) = await vehicleService.CreateVehicleAsync(request, currentUserId: 10, isAdmin: false);

        Assert.False(success);
        Assert.Contains("đã được đăng ký bởi một người dùng khác", message);
    }

    [Fact]
    public async Task CreateVehicle_UserClaimGuestVehicle_IsBlockedForSecurity()
    {
        var (vehicleService, _, context) = await CreateServicesAsync();
        // Existing guest vehicle without account
        context.Vehicles.Add(new Vehicle { PlateNumber = "51A-33333", UserId = null, IsActive = true });
        await context.SaveChangesAsync();

        var request = new CreateVehicleRequestDto { PlateNumber = "51A-33333", Brand = "Honda", Model = "Civic" };
        var (success, message, vehicle) = await vehicleService.CreateVehicleAsync(request, currentUserId: 10, isAdmin: false);

        // Security check: Regular user must NOT be allowed to claim a guest vehicle without administrative verification
        Assert.False(success);
        Assert.Null(vehicle);
        Assert.Contains("tạm khóa để bảo mật quyền sở hữu", message);

        var dbVehicle = context.Vehicles.FirstOrDefault(v => v.PlateNumber == "51A-33333");
        Assert.NotNull(dbVehicle);
        Assert.Null(dbVehicle.UserId); // Still guest vehicle
    }

    [Fact]
    public async Task CreateVehicle_AdminAssignGuestVehicle_Success()
    {
        var (vehicleService, _, context) = await CreateServicesAsync();
        context.Vehicles.Add(new Vehicle { PlateNumber = "51A-33333", UserId = null, IsActive = true });
        await context.SaveChangesAsync();

        var request = new CreateVehicleRequestDto { PlateNumber = "51A-33333", UserId = 10, Brand = "Honda", Model = "Civic" };
        var (success, message, vehicle) = await vehicleService.CreateVehicleAsync(request, currentUserId: 1, isAdmin: true);

        // Admin can assign/link guest vehicle to user
        Assert.True(success);
        Assert.NotNull(vehicle);
        Assert.Equal(10, vehicle.UserId);

        var dbVehicle = context.Vehicles.FirstOrDefault(v => v.PlateNumber == "51A-33333");
        Assert.NotNull(dbVehicle);
        Assert.Equal(10, dbVehicle.UserId);
    }

    [Fact]
    public async Task GetVehicles_User_OnlySeesOwnVehicles()
    {
        var (vehicleService, _, context) = await CreateServicesAsync();
        context.Vehicles.Add(new Vehicle { PlateNumber = "51A-USER1", UserId = 10, IsActive = true });
        context.Vehicles.Add(new Vehicle { PlateNumber = "51A-USER2", UserId = 20, IsActive = true });
        context.Vehicles.Add(new Vehicle { PlateNumber = "51A-GUEST", UserId = null, IsActive = true });
        await context.SaveChangesAsync();

        var result = await vehicleService.GetVehiclesAsync(new VehicleFilterDto(), currentUserId: 10, isAdmin: false);

        Assert.Single(result.Items);
        Assert.Equal("51A-USER1", result.Items[0].PlateNumber);
    }

    [Fact]
    public async Task GetVehicles_Admin_CanSeeAllVehicles()
    {
        var (vehicleService, _, context) = await CreateServicesAsync();
        context.Vehicles.Add(new Vehicle { PlateNumber = "51A-V1", UserId = 10, IsActive = true });
        context.Vehicles.Add(new Vehicle { PlateNumber = "51A-V2", UserId = 20, IsActive = true });
        context.Vehicles.Add(new Vehicle { PlateNumber = "51A-V3", UserId = null, IsActive = true });
        await context.SaveChangesAsync();

        var result = await vehicleService.GetVehiclesAsync(new VehicleFilterDto(), currentUserId: 1, isAdmin: true);

        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task UpdateVehicle_NotOwner_ReturnsFailure()
    {
        var (vehicleService, _, context) = await CreateServicesAsync();
        var vehicle = new Vehicle { PlateNumber = "51A-NOTMINE", UserId = 20, IsActive = true };
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        var (success, message, _) = await vehicleService.UpdateVehicleAsync(
            vehicle.Id,
            new UpdateVehicleRequestDto { Brand = "Ford" },
            currentUserId: 10,
            isAdmin: false);

        Assert.False(success);
        Assert.Contains("không có quyền", message);
    }

    [Fact]
    public async Task DeleteVehicle_WithHistory_ReturnsFailure()
    {
        var (vehicleService, _, context) = await CreateServicesAsync();
        var vehicle = new Vehicle { PlateNumber = "51A-HIST", UserId = 10, IsActive = true };
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();

        var lot = new ParkingLot { Name = "Bãi 1", TotalCapacity = 100, IsActive = true };
        context.ParkingLots.Add(lot);
        await context.SaveChangesAsync();

        context.ParkingSessions.Add(new ParkingSession
        {
            LotId = lot.Id,
            VehicleId = vehicle.Id,
            PlateNumber = vehicle.PlateNumber,
            Status = SessionStatuses.Completed,
            CheckInTime = DateTime.UtcNow.AddHours(-2),
            CheckOutTime = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var (success, message) = await vehicleService.DeleteVehicleAsync(vehicle.Id, currentUserId: 10, isAdmin: false);

        Assert.False(success);
        Assert.Contains("đã có lịch sử gửi xe", message);
    }

    #endregion

    #region ParkingSessionService Tests

    [Fact]
    public async Task CheckIn_ValidData_CreatesSessionAndLeavesSlotEmptyUntilPhysicalDetection()
    {
        var (_, sessionService, context) = await CreateServicesAsync();

        var lot = new ParkingLot { Name = "Bãi Đỗ Xe Quận 1", TotalCapacity = 50, IsActive = true };
        context.ParkingLots.Add(lot);
        await context.SaveChangesAsync();

        var floor = new ParkingFloor { LotId = lot.Id, FloorNumber = 1, Name = "Tầng 1" };
        context.ParkingFloors.Add(floor);
        await context.SaveChangesAsync();

        var slot = new ParkingSlot { FloorId = floor.Id, SlotCode = "A-01", PhysicalStatus = SlotPhysicalStatuses.Empty, IsActive = true };
        context.ParkingSlots.Add(slot);
        await context.SaveChangesAsync();

        var request = new CheckInRequestDto
        {
            LotId = lot.Id,
            PlateNumber = " 51A-99999 ",
            SlotId = slot.Id,
            CheckInImageUrl = "https://cdn.test/checkin.jpg"
        };

        var (success, message, session) = await sessionService.CheckInAsync(request, actorId: 1, actorRole: Roles.Admin, ipAddress: "127.0.0.1");

        Assert.True(success);
        Assert.NotNull(session);
        Assert.Equal("51A-99999", session.PlateNumber);
        Assert.Equal(SessionStatuses.Active, session.Status);
        Assert.Equal(slot.Id, session.CurrentSlotId);

        // Core Invariant (03_BUSINESS_RULES.md): Check-in tại cổng KHÔNG tự ý đánh dấu OCCUPIED cho ô đỗ
        // Ô đỗ vật lý vẫn là EMPTY cho tới khi cảm biến IoT hoặc module Dispatch ghi nhận xe đã đỗ thực tế
        var dbSlot = context.ParkingSlots.First(s => s.Id == slot.Id);
        Assert.Equal(SlotPhysicalStatuses.Empty, dbSlot.PhysicalStatus);

        // Verify auto-created vehicle
        var dbVehicle = context.Vehicles.FirstOrDefault(v => v.PlateNumber == "51A-99999");
        Assert.NotNull(dbVehicle);
        Assert.Null(dbVehicle.UserId); // Guest vehicle

        // Verify audit log
        var audit = context.AuditLogs.FirstOrDefault(a => a.Action == "CHECK_IN_SESSION");
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task CheckIn_InactiveLot_ReturnsFailure()
    {
        var (_, sessionService, context) = await CreateServicesAsync();

        var lot = new ParkingLot { Name = "Bãi Đang Đóng", TotalCapacity = 50, IsActive = false };
        context.ParkingLots.Add(lot);
        await context.SaveChangesAsync();

        var request = new CheckInRequestDto { LotId = lot.Id, PlateNumber = "51A-12345" };
        var (success, message, _) = await sessionService.CheckInAsync(request, actorId: 1, actorRole: Roles.Admin, ipAddress: null);

        Assert.False(success);
        Assert.Contains("hiện đang đóng hoặc tạm ngừng hoạt động", message);
    }

    [Fact]
    public async Task CheckIn_AntiPassback_DuplicateActiveSession_ReturnsConflict()
    {
        var (_, sessionService, context) = await CreateServicesAsync();

        var lot = new ParkingLot { Name = "Bãi A", TotalCapacity = 50, IsActive = true };
        context.ParkingLots.Add(lot);
        await context.SaveChangesAsync();

        context.ParkingSessions.Add(new ParkingSession
        {
            LotId = lot.Id,
            PlateNumber = "51A-ACTIVE",
            Status = SessionStatuses.Active,
            CheckInTime = DateTime.UtcNow.AddMinutes(-30)
        });
        await context.SaveChangesAsync();

        var request = new CheckInRequestDto { LotId = lot.Id, PlateNumber = "51A-ACTIVE" };
        var (success, message, _) = await sessionService.CheckInAsync(request, actorId: 1, actorRole: Roles.Admin, ipAddress: null);

        Assert.False(success);
        Assert.Contains("đang có phiên đỗ xe chưa kết thúc (ACTIVE/PAID)", message);
    }

    [Fact]
    public async Task CheckIn_AntiPassback_DuplicatePaidSession_ReturnsConflict()
    {
        var (_, sessionService, context) = await CreateServicesAsync();

        var lot = new ParkingLot { Name = "Bãi B", TotalCapacity = 50, IsActive = true };
        context.ParkingLots.Add(lot);
        await context.SaveChangesAsync();

        context.ParkingSessions.Add(new ParkingSession
        {
            LotId = lot.Id,
            PlateNumber = "51A-PAID",
            Status = SessionStatuses.Paid,
            CheckInTime = DateTime.UtcNow.AddMinutes(-40)
        });
        await context.SaveChangesAsync();

        var request = new CheckInRequestDto { LotId = lot.Id, PlateNumber = "51A-PAID" };
        var (success, message, _) = await sessionService.CheckInAsync(request, actorId: 1, actorRole: Roles.Admin, ipAddress: null);

        Assert.False(success);
        Assert.Contains("đang có phiên đỗ xe chưa kết thúc (ACTIVE/PAID)", message);
    }

    [Fact]
    public async Task CheckIn_CompletedPreviousSession_AllowsNewCheckIn()
    {
        var (_, sessionService, context) = await CreateServicesAsync();

        var lot = new ParkingLot { Name = "Bãi C", TotalCapacity = 50, IsActive = true };
        context.ParkingLots.Add(lot);
        await context.SaveChangesAsync();

        // Completed session in the past
        context.ParkingSessions.Add(new ParkingSession
        {
            LotId = lot.Id,
            PlateNumber = "51A-COMPLETED",
            Status = SessionStatuses.Completed,
            CheckInTime = DateTime.UtcNow.AddHours(-3),
            CheckOutTime = DateTime.UtcNow.AddHours(-1)
        });
        await context.SaveChangesAsync();

        var request = new CheckInRequestDto { LotId = lot.Id, PlateNumber = "51A-COMPLETED" };
        var (success, message, session) = await sessionService.CheckInAsync(request, actorId: 1, actorRole: Roles.Admin, ipAddress: null);

        Assert.True(success);
        Assert.NotNull(session);
        Assert.Equal(SessionStatuses.Active, session.Status);
    }

    [Fact]
    public async Task CheckOut_ActiveSession_CompletesAndReleasesSlot()
    {
        var (_, sessionService, context) = await CreateServicesAsync();

        var lot = new ParkingLot { Name = "Bãi D", TotalCapacity = 50, IsActive = true };
        context.ParkingLots.Add(lot);
        await context.SaveChangesAsync();

        var floor = new ParkingFloor { LotId = lot.Id, FloorNumber = 1, Name = "Tầng 1" };
        context.ParkingFloors.Add(floor);
        await context.SaveChangesAsync();

        var slot = new ParkingSlot { FloorId = floor.Id, SlotCode = "D-01", PhysicalStatus = SlotPhysicalStatuses.Occupied, IsActive = true };
        context.ParkingSlots.Add(slot);
        await context.SaveChangesAsync();

        var session = new ParkingSession
        {
            LotId = lot.Id,
            CurrentSlotId = slot.Id,
            PlateNumber = "51A-OUT",
            Status = SessionStatuses.Active,
            CheckInTime = DateTime.UtcNow.AddMinutes(-60)
        };
        context.ParkingSessions.Add(session);
        await context.SaveChangesAsync();

        var request = new CheckOutRequestDto { CheckOutImageUrl = "https://cdn.test/checkout.jpg" };
        var (success, message, updatedSession) = await sessionService.CheckOutAsync(session.Id, request, actorId: 1, actorRole: Roles.Admin, ipAddress: "127.0.0.1");

        Assert.True(success);
        Assert.NotNull(updatedSession);
        Assert.Equal(SessionStatuses.Completed, updatedSession.Status);
        Assert.NotNull(updatedSession.CheckOutTime);

        // Verify slot is freed to EMPTY
        var dbSlot = context.ParkingSlots.First(s => s.Id == slot.Id);
        Assert.Equal(SlotPhysicalStatuses.Empty, dbSlot.PhysicalStatus);

        // Verify audit log
        var audit = context.AuditLogs.FirstOrDefault(a => a.Action == "CHECK_OUT_SESSION");
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task CheckOut_AlreadyCompleted_ReturnsFailure()
    {
        var (_, sessionService, context) = await CreateServicesAsync();

        var lot = new ParkingLot { Name = "Bãi E", TotalCapacity = 50, IsActive = true };
        context.ParkingLots.Add(lot);
        await context.SaveChangesAsync();

        var session = new ParkingSession
        {
            LotId = lot.Id,
            PlateNumber = "51A-DONE",
            Status = SessionStatuses.Completed,
            CheckInTime = DateTime.UtcNow.AddHours(-2),
            CheckOutTime = DateTime.UtcNow.AddHours(-1)
        };
        context.ParkingSessions.Add(session);
        await context.SaveChangesAsync();

        var (success, message, _) = await sessionService.CheckOutAsync(session.Id, new CheckOutRequestDto(), actorId: 1, actorRole: Roles.Admin, ipAddress: null);

        Assert.False(success);
        Assert.Contains("đã kết thúc trước đó", message);
    }

    [Fact]
    public async Task GetUserSessions_User_OnlySeesOwnVehicleSessions()
    {
        var (_, sessionService, context) = await CreateServicesAsync();

        var lot = new ParkingLot { Name = "Bãi F", TotalCapacity = 50, IsActive = true };
        context.ParkingLots.Add(lot);
        await context.SaveChangesAsync();

        var vUser10 = new Vehicle { PlateNumber = "51A-MYCAR", UserId = 10, IsActive = true };
        var vUser20 = new Vehicle { PlateNumber = "51A-OTHERCAR", UserId = 20, IsActive = true };
        context.Vehicles.AddRange(vUser10, vUser20);
        await context.SaveChangesAsync();

        context.ParkingSessions.Add(new ParkingSession { LotId = lot.Id, VehicleId = vUser10.Id, PlateNumber = vUser10.PlateNumber, Status = SessionStatuses.Active });
        context.ParkingSessions.Add(new ParkingSession { LotId = lot.Id, VehicleId = vUser20.Id, PlateNumber = vUser20.PlateNumber, Status = SessionStatuses.Active });
        await context.SaveChangesAsync();

        var result = await sessionService.GetUserSessionsAsync(userId: 10, new ParkingSessionFilterDto());

        Assert.Single(result.Items);
        Assert.Equal("51A-MYCAR", result.Items[0].PlateNumber);
    }

    #endregion

    #region Controller Authorization Attributes Verification

    [Fact]
    public void VehiclesController_HasCorrectAuthorizationPolicies()
    {
        var controllerAuth = typeof(VehiclesController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(controllerAuth);
        Assert.Contains(Roles.Admin, controllerAuth.Roles);
        Assert.Contains(Roles.User, controllerAuth.Roles);
    }

    [Fact]
    public void ParkingSessionsController_HasCorrectRoleRestrictions()
    {
        var checkInMethod = typeof(ParkingSessionsController).GetMethod(nameof(ParkingSessionsController.CheckIn));
        var checkInAuth = checkInMethod?.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(checkInAuth);
        Assert.Equal($"{Roles.Admin},{Roles.Manager},{Roles.Staff}", checkInAuth.Roles);

        var checkOutMethod = typeof(ParkingSessionsController).GetMethod(nameof(ParkingSessionsController.CheckOut));
        var checkOutAuth = checkOutMethod?.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(checkOutAuth);
        Assert.Equal($"{Roles.Admin},{Roles.Manager},{Roles.Staff}", checkOutAuth.Roles);

        var activeMethod = typeof(ParkingSessionsController).GetMethod(nameof(ParkingSessionsController.GetActiveSessions));
        var activeAuth = activeMethod?.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(activeAuth);
        Assert.Equal($"{Roles.Admin},{Roles.Manager},{Roles.Staff}", activeAuth.Roles);

        var mySessionsMethod = typeof(ParkingSessionsController).GetMethod(nameof(ParkingSessionsController.GetMySessions));
        var mySessionsAuth = mySessionsMethod?.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(mySessionsAuth);
        Assert.Contains(Roles.User, mySessionsAuth.Roles);
        Assert.Contains(Roles.Admin, mySessionsAuth.Roles);
    }

    #endregion
}
