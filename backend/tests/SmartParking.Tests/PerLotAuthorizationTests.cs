using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.Application.DTOs.Dispatches;
using SmartParking.Application.DTOs.Sessions;
using SmartParking.Domain.Constants;
using SmartParking.Domain.Entities;
using SmartParking.Infrastructure.Services;
using Xunit;

namespace SmartParking.Tests;

public class PerLotAuthorizationTests
{
    private async Task<(
        ParkingAuthorizationService AuthService,
        ParkingSessionService SessionService,
        ParkingDispatchService DispatchService,
        TestSmartParkingDbContext Context,
        User Admin,
        User StaffLot1,
        User StaffLot2,
        User UnassignedStaff,
        User RevokedStaff,
        ParkingLot Lot1,
        ParkingLot Lot2,
        ParkingSlot SlotLot1,
        ParkingSlot SlotLot2,
        Vehicle Vehicle1
    )> SetupPerLotEnvironmentAsync()
    {
        var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var authService = new ParkingAuthorizationService(context);
        var sessionService = new ParkingSessionService(context, authService, NullLogger<ParkingSessionService>.Instance);
        var dispatchService = new ParkingDispatchService(context, authService, NullLogger<ParkingDispatchService>.Instance);

        var admin = new User { Id = 1, Username = "admin", Email = "admin@test.com", FullName = "Admin", PasswordHash = "h", RoleId = 1, IsActive = true };
        var staff1 = new User { Id = 2, Username = "staff1", Email = "staff1@test.com", FullName = "Staff 1", PasswordHash = "h", RoleId = 3, IsActive = true };
        var staff2 = new User { Id = 3, Username = "staff2", Email = "staff2@test.com", FullName = "Staff 2", PasswordHash = "h", RoleId = 3, IsActive = true };
        var unassigned = new User { Id = 4, Username = "unassigned", Email = "unassigned@test.com", FullName = "Unassigned", PasswordHash = "h", RoleId = 3, IsActive = true };
        var revoked = new User { Id = 5, Username = "revoked", Email = "revoked@test.com", FullName = "Revoked", PasswordHash = "h", RoleId = 3, IsActive = true };

        var lot1 = new ParkingLot { Id = 1, Name = "Bãi 1", Address = "Q1", TotalCapacity = 50, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var lot2 = new ParkingLot { Id = 2, Name = "Bãi 2", Address = "Q2", TotalCapacity = 50, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

        var floor1 = new ParkingFloor { Id = 1, LotId = 1, FloorNumber = 1, Name = "Tầng 1 - Bãi 1", TotalSlots = 10, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var floor2 = new ParkingFloor { Id = 2, LotId = 2, FloorNumber = 1, Name = "Tầng 1 - Bãi 2", TotalSlots = 10, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

        var slot1 = new ParkingSlot { Id = 1, FloorId = 1, SlotCode = "B1-01", PhysicalStatus = SlotPhysicalStatuses.Empty, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Floor = floor1 };
        var slot2 = new ParkingSlot { Id = 2, FloorId = 2, SlotCode = "B2-01", PhysicalStatus = SlotPhysicalStatuses.Empty, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Floor = floor2 };

        var vehicle1 = new Vehicle { Id = 1, PlateNumber = "51A-88888", UserId = 100, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

        // Staff 1 assigned to Lot 1 (active)
        var assignmentStaff1 = new StaffLotAssignment
        {
            Id = 1,
            UserId = staff1.Id,
            LotId = lot1.Id,
            AssignedBy = admin.Id,
            AssignedAt = DateTime.UtcNow,
            IsActive = true,
            Duty = StaffDuties.Both
        };

        // Staff 2 assigned to Lot 2 (active)
        var assignmentStaff2 = new StaffLotAssignment
        {
            Id = 2,
            UserId = staff2.Id,
            LotId = lot2.Id,
            AssignedBy = admin.Id,
            AssignedAt = DateTime.UtcNow,
            IsActive = true,
            Duty = StaffDuties.Both
        };

        // Revoked Staff assignment to Lot 1 (inactive)
        var assignmentRevoked = new StaffLotAssignment
        {
            Id = 3,
            UserId = revoked.Id,
            LotId = lot1.Id,
            AssignedBy = admin.Id,
            AssignedAt = DateTime.UtcNow.AddDays(-5),
            IsActive = false,
            RevokedBy = admin.Id,
            RevokedAt = DateTime.UtcNow.AddDays(-1),
            RevocationReason = "Đã thu hồi"
        };

        context.Users.AddRange(admin, staff1, staff2, unassigned, revoked);
        context.ParkingLots.AddRange(lot1, lot2);
        context.ParkingFloors.AddRange(floor1, floor2);
        context.ParkingSlots.AddRange(slot1, slot2);
        context.Vehicles.Add(vehicle1);
        context.StaffLotAssignments.AddRange(assignmentStaff1, assignmentStaff2, assignmentRevoked);
        await context.SaveChangesAsync();

        return (authService, sessionService, dispatchService, context, admin, staff1, staff2, unassigned, revoked, lot1, lot2, slot1, slot2, vehicle1);
    }

    #region ParkingAuthorizationService Unit Tests

    [Fact]
    public async Task AuthService_Admin_HasGlobalAccess()
    {
        var (auth, _, _, _, admin, _, _, _, _, lot1, lot2, _, _, _) = await SetupPerLotEnvironmentAsync();

        Assert.True(await auth.HasLotAccessAsync(admin.Id, Roles.Admin, lot1.Id));
        Assert.True(await auth.HasLotAccessAsync(admin.Id, Roles.Admin, lot2.Id));
        Assert.True(await auth.CanManageLotStatusAsync(admin.Id, Roles.Admin, lot1.Id));
        Assert.True(await auth.CanManageSlotStatusAsync(admin.Id, Roles.Admin, lot1.Id));
        Assert.True(await auth.CanOperateSessionOrDispatchAsync(admin.Id, Roles.Admin, lot1.Id));
    }

    [Fact]
    public async Task AuthService_Staff_AllowedOnAssignedLot_ForbiddenOnOtherOrUnassignedOrRevoked()
    {
        var (auth, _, _, _, _, staff1, staff2, unassigned, revoked, lot1, lot2, _, _, _) = await SetupPerLotEnvironmentAsync();

        // Staff 1 assigned to Lot 1
        Assert.True(await auth.HasLotAccessAsync(staff1.Id, Roles.Staff, lot1.Id));
        Assert.False(await auth.HasLotAccessAsync(staff1.Id, Roles.Staff, lot2.Id));

        // Staff 2 assigned to Lot 2
        Assert.True(await auth.HasLotAccessAsync(staff2.Id, Roles.Staff, lot2.Id));
        Assert.False(await auth.HasLotAccessAsync(staff2.Id, Roles.Staff, lot1.Id));

        // Unassigned Staff has NO access anywhere
        Assert.False(await auth.HasLotAccessAsync(unassigned.Id, Roles.Staff, lot1.Id));
        Assert.False(await auth.HasLotAccessAsync(unassigned.Id, Roles.Staff, lot2.Id));

        // Revoked Staff has NO access
        Assert.False(await auth.HasLotAccessAsync(revoked.Id, Roles.Staff, lot1.Id));
    }

    [Fact]
    public async Task AuthService_RoleSeparation_StaffCannotManageLotOrSlotStatus()
    {
        var (auth, _, _, _, _, staff1, _, _, _, lot1, _, _, _, _) = await SetupPerLotEnvironmentAsync();

        // Staff assigned to Lot 1 can operate check-in/dispatch
        Assert.True(await auth.CanOperateSessionOrDispatchAsync(staff1.Id, Roles.Staff, lot1.Id));

        // But Staff cannot manage lot status or slot physical maintenance status (Manager/Admin only)
        Assert.False(await auth.CanManageLotStatusAsync(staff1.Id, Roles.Staff, lot1.Id));
        Assert.False(await auth.CanManageSlotStatusAsync(staff1.Id, Roles.Staff, lot1.Id));
    }

    #endregion

    #region ParkingSession Per-Lot Isolation Tests

    [Fact]
    public async Task CheckIn_AssignedStaff_Allowed()
    {
        var (_, sessionService, _, _, _, staff1, _, _, _, lot1, _, slot1, _, _) = await SetupPerLotEnvironmentAsync();

        var (success, msg, session) = await sessionService.CheckInAsync(new CheckInRequestDto
        {
            LotId = lot1.Id,
            PlateNumber = "51A-11111",
            SlotId = slot1.Id
        }, staff1.Id, Roles.Staff, "127.0.0.1");

        Assert.True(success);
        Assert.NotNull(session);
        Assert.Equal(lot1.Id, session.LotId);
    }

    [Fact]
    public async Task CheckIn_DifferentLotStaff_Forbidden()
    {
        var (_, sessionService, _, _, _, _, staff2, _, _, lot1, _, slot1, _, _) = await SetupPerLotEnvironmentAsync();

        // Staff 2 (assigned to Lot 2) attempts check-in at Lot 1
        var (success, msg, session) = await sessionService.CheckInAsync(new CheckInRequestDto
        {
            LotId = lot1.Id,
            PlateNumber = "51A-22222",
            SlotId = slot1.Id
        }, staff2.Id, Roles.Staff, "127.0.0.1");

        Assert.False(success);
        Assert.Null(session);
        Assert.Contains("không có quyền", msg);
    }

    [Fact]
    public async Task CheckIn_UnassignedStaff_Forbidden()
    {
        var (_, sessionService, _, _, _, _, _, unassigned, _, lot1, _, slot1, _, _) = await SetupPerLotEnvironmentAsync();

        var (success, msg, _) = await sessionService.CheckInAsync(new CheckInRequestDto
        {
            LotId = lot1.Id,
            PlateNumber = "51A-33333",
            SlotId = slot1.Id
        }, unassigned.Id, Roles.Staff, "127.0.0.1");

        Assert.False(success);
        Assert.Contains("không có quyền", msg);
    }

    [Fact]
    public async Task CheckIn_RevokedStaff_ForbiddenImmediately()
    {
        var (_, sessionService, _, _, _, _, _, _, revoked, lot1, _, slot1, _, _) = await SetupPerLotEnvironmentAsync();

        var (success, msg, _) = await sessionService.CheckInAsync(new CheckInRequestDto
        {
            LotId = lot1.Id,
            PlateNumber = "51A-44444",
            SlotId = slot1.Id
        }, revoked.Id, Roles.Staff, "127.0.0.1");

        Assert.False(success);
        Assert.Contains("không có quyền", msg);
    }

    [Fact]
    public async Task CheckOut_CrossLotStaff_Forbidden()
    {
        var (_, sessionService, _, context, admin, staff1, staff2, _, _, lot1, _, slot1, _, _) = await SetupPerLotEnvironmentAsync();

        // Tạo phiên tại Lot 1
        var (sIn, _, session) = await sessionService.CheckInAsync(new CheckInRequestDto
        {
            LotId = lot1.Id,
            PlateNumber = "51A-55555",
            SlotId = slot1.Id
        }, staff1.Id, Roles.Staff, "127.0.0.1");
        Assert.True(sIn);

        // Staff 2 (Lot 2) cố gắng check-out phiên ở Lot 1 -> Bị từ chối
        var (sOut2, msgOut2, _) = await sessionService.CheckOutAsync(session!.Id, new CheckOutRequestDto(), staff2.Id, Roles.Staff, "127.0.0.1");
        Assert.False(sOut2);
        Assert.Contains("không có quyền", msgOut2);

        // Staff 1 (Lot 1) check-out -> Thành công khi phiên đã hoàn tất thanh toán
        var sessionEntity = await context.ParkingSessions.FindAsync(session.Id);
        sessionEntity!.Status = SessionStatuses.Paid;
        sessionEntity.PaidAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var (sOut1, _, _) = await sessionService.CheckOutAsync(session.Id, new CheckOutRequestDto(), staff1.Id, Roles.Staff, "127.0.0.1");
        Assert.True(sOut1);
    }

    #endregion

    #region ParkingDispatch Per-Lot Isolation Tests

    [Fact]
    public async Task CreateDispatch_CrossLotStaff_Forbidden()
    {
        var (_, sessionService, dispatchService, _, _, staff1, staff2, _, _, lot1, _, slot1, _, _) = await SetupPerLotEnvironmentAsync();

        var (_, _, session) = await sessionService.CheckInAsync(new CheckInRequestDto
        {
            LotId = lot1.Id,
            PlateNumber = "51A-66666"
        }, staff1.Id, Roles.Staff, "127.0.0.1");

        // Staff 2 (Lot 2) cố gắng tạo dispatch cho phiên tại Lot 1 -> Bị từ chối
        var (sDisp2, msgDisp2, _) = await dispatchService.CreateDispatchAsync(new CreateDispatchRequestDto
        {
            SessionId = session!.Id,
            AssignedSlotId = slot1.Id
        }, staff2.Id, Roles.Staff, "127.0.0.1");

        Assert.False(sDisp2);
        Assert.Contains("không có quyền", msgDisp2);

        // Staff 1 (Lot 1) tạo dispatch -> Thành công
        var (sDisp1, _, d1) = await dispatchService.CreateDispatchAsync(new CreateDispatchRequestDto
        {
            SessionId = session.Id,
            AssignedSlotId = slot1.Id
        }, staff1.Id, Roles.Staff, "127.0.0.1");

        Assert.True(sDisp1);
        Assert.NotNull(d1);
    }

    [Fact]
    public async Task Reassign_Confirm_WrongSlot_Cancel_CrossLotStaff_Forbidden()
    {
        var (_, sessionService, dispatchService, context, _, staff1, staff2, _, _, lot1, _, slot1, _, _) = await SetupPerLotEnvironmentAsync();

        var (_, _, session) = await sessionService.CheckInAsync(new CheckInRequestDto
        {
            LotId = lot1.Id,
            PlateNumber = "51A-77777"
        }, staff1.Id, Roles.Staff, "127.0.0.1");

        var (sDisp, _, d1) = await dispatchService.CreateDispatchAsync(new CreateDispatchRequestDto
        {
            SessionId = session!.Id,
            AssignedSlotId = slot1.Id
        }, staff1.Id, Roles.Staff, "127.0.0.1");
        Assert.True(sDisp);

        // Staff 2 cố gắng Reassign -> Bị từ chối
        var (sReassign, msgReassign, _) = await dispatchService.ReassignSlotAsync(d1!.Id, new AssignSlotRequestDto { SlotId = slot1.Id }, staff2.Id, Roles.Staff, "127.0.0.1");
        Assert.False(sReassign);
        Assert.Contains("không có quyền", msgReassign);

        // Staff 2 cố gắng ConfirmOccupied -> Bị từ chối
        var (sConfirm, msgConfirm, _) = await dispatchService.ConfirmOccupiedAsync(d1.Id, new ConfirmOccupiedRequestDto(), staff2.Id, Roles.Staff, "127.0.0.1");
        Assert.False(sConfirm);
        Assert.Contains("không có quyền", msgConfirm);

        // Staff 2 cố gắng ReportWrongSlot -> Bị từ chối
        var (sWrong, msgWrong, _) = await dispatchService.ReportWrongSlotAsync(d1.Id, new ReportWrongSlotRequestDto { ActualSlotId = slot1.Id }, staff2.Id, Roles.Staff, "127.0.0.1");
        Assert.False(sWrong);
        Assert.Contains("không có quyền", msgWrong);

        // Staff 2 cố gắng CancelDispatch -> Bị từ chối
        var (sCancel, msgCancel) = await dispatchService.CancelDispatchAsync(d1.Id, new CancelDispatchRequestDto { Reason = "test" }, staff2.Id, Roles.Staff, "127.0.0.1");
        Assert.False(sCancel);
        Assert.Contains("không có quyền", msgCancel);
    }

    [Fact]
    public async Task ScopeLeakage_SlotFromOtherLot_Rejected()
    {
        var (_, sessionService, dispatchService, _, _, staff1, _, _, _, lot1, _, slot1, slot2, _) = await SetupPerLotEnvironmentAsync();

        // 1. Check-in ở Lot 1 nhưng truyền slotId của Lot 2 -> Bị từ chối
        var (sCheckIn, msgCheckIn, _) = await sessionService.CheckInAsync(new CheckInRequestDto
        {
            LotId = lot1.Id,
            PlateNumber = "51A-88889",
            SlotId = slot2.Id // Slot of Lot 2!
        }, staff1.Id, Roles.Staff, "127.0.0.1");

        Assert.False(sCheckIn);
        Assert.Contains("không thuộc bãi đỗ xe", msgCheckIn);

        // 2. Dispatch ở Lot 1 nhưng truyền slotId của Lot 2 -> Bị từ chối
        var (_, _, session) = await sessionService.CheckInAsync(new CheckInRequestDto
        {
            LotId = lot1.Id,
            PlateNumber = "51A-88889"
        }, staff1.Id, Roles.Staff, "127.0.0.1");

        var (sDisp, msgDisp, _) = await dispatchService.CreateDispatchAsync(new CreateDispatchRequestDto
        {
            SessionId = session!.Id,
            AssignedSlotId = slot2.Id // Slot of Lot 2!
        }, staff1.Id, Roles.Staff, "127.0.0.1");

        Assert.False(sDisp);
        Assert.Contains("không thuộc bãi đỗ xe", msgDisp);
    }

    [Fact]
    public async Task StaleJwtRole_DemotedInDatabase_AccessDenied()
    {
        var (auth, _, _, context, _, staff1, _, _, _, lot1, _, _, _, _) = await SetupPerLotEnvironmentAsync();

        // Ban đầu Staff 1 có quyền tại Lot 1
        Assert.True(await auth.HasLotAccessAsync(staff1.Id, Roles.Staff, lot1.Id));

        // Admin giáng cấp Staff 1 thành USER trong database
        var dbUser = await context.Users.FindAsync(staff1.Id);
        dbUser!.RoleId = 4; // USER role
        await context.SaveChangesAsync();

        // Request tiếp theo gửi JWT claim cũ (Roles.Staff) -> Phải bị từ chối vì database role là USER
        var hasAccess = await auth.HasLotAccessAsync(staff1.Id, Roles.Staff, lot1.Id);
        Assert.False(hasAccess);

        var canOperate = await auth.CanOperateSessionOrDispatchAsync(staff1.Id, Roles.Staff, lot1.Id);
        Assert.False(canOperate);
    }

    [Fact]
    public async Task StaleJwtRole_DisabledAccountInDatabase_AccessDenied()
    {
        var (auth, _, _, context, _, staff1, _, _, _, lot1, _, _, _, _) = await SetupPerLotEnvironmentAsync();

        // Vô hiệu hóa tài khoản Staff 1 trong database
        var dbUser = await context.Users.FindAsync(staff1.Id);
        dbUser!.IsActive = false;
        await context.SaveChangesAsync();

        // Request gửi JWT claim còn hạn (Roles.Staff) -> Phải bị từ chối vì tài khoản đã bị khóa trong database
        var hasAccess = await auth.HasLotAccessAsync(staff1.Id, Roles.Staff, lot1.Id);
        Assert.False(hasAccess);
    }

    #endregion
}
