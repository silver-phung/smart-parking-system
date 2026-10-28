using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.Application.DTOs.StaffAssignments;
using SmartParking.Domain.Constants;
using SmartParking.Domain.Entities;
using SmartParking.Infrastructure.Services;
using Xunit;

namespace SmartParking.Tests;

public class StaffLotAssignmentTests
{
    private async Task<(StaffLotAssignmentService Service, TestSmartParkingDbContext Context, User Admin, User Manager, User Staff, User RegularUser, ParkingLot Lot1, ParkingLot Lot2)> SetupContextAsync()
    {
        var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var service = new StaffLotAssignmentService(context, NullLogger<StaffLotAssignmentService>.Instance);

        var admin = new User
        {
            Id = 1,
            Username = "admin_test",
            Email = "admin@smartparking.test",
            FullName = "Admin Tester",
            PasswordHash = "hash",
            RoleId = 1,
            IsActive = true,
            ApprovalStatus = "APPROVED"
        };
        var manager = new User
        {
            Id = 2,
            Username = "manager_test",
            Email = "manager@smartparking.test",
            FullName = "Manager Tester",
            PasswordHash = "hash",
            RoleId = 2,
            IsActive = true,
            ApprovalStatus = "APPROVED"
        };
        var staff = new User
        {
            Id = 3,
            Username = "staff_test",
            Email = "staff@smartparking.test",
            FullName = "Staff Tester",
            PasswordHash = "hash",
            RoleId = 3,
            IsActive = true,
            ApprovalStatus = "APPROVED"
        };
        var user = new User
        {
            Id = 4,
            Username = "user_test",
            Email = "user@smartparking.test",
            FullName = "Regular User",
            PasswordHash = "hash",
            RoleId = 4,
            IsActive = true,
            ApprovalStatus = "APPROVED"
        };

        var lot1 = new ParkingLot
        {
            Id = 1,
            Name = "Bãi Đỗ Xe Quận 1",
            Address = "1 Lê Duẩn",
            TotalCapacity = 100,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var lot2 = new ParkingLot
        {
            Id = 2,
            Name = "Bãi Đỗ Xe Quận 3",
            Address = "2 Nam Kỳ Khởi Nghĩa",
            TotalCapacity = 50,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Users.AddRange(admin, manager, staff, user);
        context.ParkingLots.AddRange(lot1, lot2);
        await context.SaveChangesAsync();

        return (service, context, admin, manager, staff, user, lot1, lot2);
    }

    [Fact]
    public async Task CreateAssignment_ValidManagerOrStaff_Succeeds()
    {
        var (service, context, admin, manager, staff, _, lot1, _) = await SetupContextAsync();

        var req1 = new CreateStaffAssignmentRequestDto
        {
            UserId = manager.Id,
            LotId = lot1.Id,
            Notes = "Phân công quản lý bãi 1"
        };
        var (s1, msg1, d1) = await service.CreateAssignmentAsync(req1, admin.Id, "127.0.0.1");

        Assert.True(s1);
        Assert.NotNull(d1);
        Assert.Equal(manager.Id, d1.UserId);
        Assert.Equal(lot1.Id, d1.LotId);
        Assert.True(d1.IsActive);
        Assert.Equal(admin.Id, d1.AssignedBy);

        var req2 = new CreateStaffAssignmentRequestDto
        {
            UserId = staff.Id,
            LotId = lot1.Id,
            Notes = "Phân công nhân viên vận hành bãi 1"
        };
        var (s2, _, d2) = await service.CreateAssignmentAsync(req2, admin.Id, "127.0.0.1");

        Assert.True(s2);
        Assert.NotNull(d2);
        Assert.Equal(staff.Id, d2.UserId);
    }

    [Fact]
    public async Task CreateAssignment_AssignRegularUser_Fails()
    {
        var (service, _, admin, _, _, regularUser, lot1, _) = await SetupContextAsync();

        var req = new CreateStaffAssignmentRequestDto
        {
            UserId = regularUser.Id,
            LotId = lot1.Id
        };
        var (s, msg, d) = await service.CreateAssignmentAsync(req, admin.Id, "127.0.0.1");

        Assert.False(s);
        Assert.Null(d);
        Assert.Contains("MANAGER hoặc STAFF", msg);
    }

    [Fact]
    public async Task CreateAssignment_InactiveLot_Fails()
    {
        var (service, context, admin, manager, _, _, lot1, _) = await SetupContextAsync();
        lot1.IsActive = false;
        await context.SaveChangesAsync();

        var req = new CreateStaffAssignmentRequestDto
        {
            UserId = manager.Id,
            LotId = lot1.Id
        };
        var (s, msg, _) = await service.CreateAssignmentAsync(req, admin.Id, "127.0.0.1");

        Assert.False(s);
        Assert.Contains("đang tạm đóng hoặc ngừng hoạt động", msg);
    }

    [Fact]
    public async Task CreateAssignment_DuplicateActiveAssignment_ReturnsConflict()
    {
        var (service, _, admin, manager, _, _, lot1, _) = await SetupContextAsync();

        var req = new CreateStaffAssignmentRequestDto
        {
            UserId = manager.Id,
            LotId = lot1.Id
        };
        var (s1, _, _) = await service.CreateAssignmentAsync(req, admin.Id, "127.0.0.1");
        Assert.True(s1);

        // Gán lần 2 trùng user và trùng bãi khi đang active
        var (s2, msg2, _) = await service.CreateAssignmentAsync(req, admin.Id, "127.0.0.1");
        Assert.False(s2);
        Assert.Contains("hiện đã được phân công", msg2);
    }

    [Fact]
    public async Task RevokeAssignment_SetsActiveFalse_AndAuditDetails()
    {
        var (service, context, admin, manager, _, _, lot1, _) = await SetupContextAsync();

        var (s1, _, created) = await service.CreateAssignmentAsync(new CreateStaffAssignmentRequestDto
        {
            UserId = manager.Id,
            LotId = lot1.Id
        }, admin.Id, "127.0.0.1");
        Assert.True(s1);

        var revokeReq = new RevokeStaffAssignmentRequestDto
        {
            Reason = "Điều chuyển sang cơ sở khác"
        };
        var (s2, msg2) = await service.RevokeAssignmentAsync(created!.Id, revokeReq, admin.Id, "127.0.0.1");
        Assert.True(s2);

        var inDb = await context.StaffLotAssignments.FindAsync(created.Id);
        Assert.NotNull(inDb);
        Assert.False(inDb.IsActive);
        Assert.Equal(admin.Id, inDb.RevokedBy);
        Assert.NotNull(inDb.RevokedAt);
        Assert.Equal("Điều chuyển sang cơ sở khác", inDb.RevocationReason);
    }

    [Fact]
    public async Task RevokeAssignment_AlreadyRevoked_Fails()
    {
        var (service, _, admin, manager, _, _, lot1, _) = await SetupContextAsync();

        var (s1, _, created) = await service.CreateAssignmentAsync(new CreateStaffAssignmentRequestDto
        {
            UserId = manager.Id,
            LotId = lot1.Id
        }, admin.Id, "127.0.0.1");
        Assert.True(s1);

        var revokeReq = new RevokeStaffAssignmentRequestDto { Reason = "Lần 1" };
        var (s2, _) = await service.RevokeAssignmentAsync(created!.Id, revokeReq, admin.Id, "127.0.0.1");
        Assert.True(s2);

        // Thu hồi lần 2
        var (s3, msg3) = await service.RevokeAssignmentAsync(created.Id, new RevokeStaffAssignmentRequestDto { Reason = "Lần 2" }, admin.Id, "127.0.0.1");
        Assert.False(s3);
        Assert.Contains("đã được thu hồi", msg3);
    }

    [Fact]
    public async Task Reassign_AfterRevocation_SucceedsWithoutConflict()
    {
        var (service, _, admin, manager, _, _, lot1, _) = await SetupContextAsync();

        var req = new CreateStaffAssignmentRequestDto
        {
            UserId = manager.Id,
            LotId = lot1.Id
        };
        var (s1, _, created) = await service.CreateAssignmentAsync(req, admin.Id, "127.0.0.1");
        Assert.True(s1);

        // Thu hồi
        await service.RevokeAssignmentAsync(created!.Id, new RevokeStaffAssignmentRequestDto { Reason = "Tạm nghỉ" }, admin.Id, "127.0.0.1");

        // Gán lại bãi cũ -> Thành công, không bị conflict nhờ partial unique index (chỉ index active = true)
        var (s2, _, reCreated) = await service.CreateAssignmentAsync(req, admin.Id, "127.0.0.1");
        Assert.True(s2);
        Assert.NotNull(reCreated);
        Assert.NotEqual(created.Id, reCreated.Id);
        Assert.True(reCreated.IsActive);
    }

    [Fact]
    public async Task GetMyAssignedLots_ReturnsOnlyActiveAssignmentsForUser()
    {
        var (service, _, admin, manager, _, _, lot1, lot2) = await SetupContextAsync();

        // Gán Lot 1
        var (s1, _, a1) = await service.CreateAssignmentAsync(new CreateStaffAssignmentRequestDto
        {
            UserId = manager.Id,
            LotId = lot1.Id
        }, admin.Id, "127.0.0.1");
        Assert.True(s1);

        // Gán Lot 2 rồi thu hồi
        var (s2, _, a2) = await service.CreateAssignmentAsync(new CreateStaffAssignmentRequestDto
        {
            UserId = manager.Id,
            LotId = lot2.Id
        }, admin.Id, "127.0.0.1");
        Assert.True(s2);

        await service.RevokeAssignmentAsync(a2!.Id, new RevokeStaffAssignmentRequestDto { Reason = "Hết hạn" }, admin.Id, "127.0.0.1");

        // Tra cứu my-lots
        var myLots = await service.GetMyAssignedLotsAsync(manager.Id);
        Assert.Single(myLots);
        Assert.Equal(lot1.Id, myLots[0].LotId);
        Assert.Equal(lot1.Name, myLots[0].LotName);
    }
}
