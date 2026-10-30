using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Common.Models;
using SmartParking.Application.DTOs.AdminUsers;
using SmartParking.Domain.Constants;
using SmartParking.Domain.Entities;
using SmartParking.Infrastructure.Data;
using SmartParking.Infrastructure.Services;
using Xunit;

namespace SmartParking.Tests;

public class AdminUserManagementTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AdminUserManagementTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(AdminUserService Service, TestSmartParkingDbContext Context, User Admin1, User Admin2, User Manager, User Staff, User RegularUser, ParkingLot Lot)> SetupServiceAsync()
    {
        var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var passwordHasher = new PasswordHasherService();
        var service = new AdminUserService(context, passwordHasher, NullLogger<AdminUserService>.Instance);

        var admin1 = new User
        {
            Id = 1,
            Username = "super_admin",
            Email = "admin1@smartparking.test",
            FullName = "Super Admin",
            PasswordHash = "hash1",
            RoleId = 1,
            IsActive = true,
            ApprovalStatus = ApprovalStatuses.Approved
        };

        var admin2 = new User
        {
            Id = 2,
            Username = "backup_admin",
            Email = "admin2@smartparking.test",
            FullName = "Backup Admin",
            PasswordHash = "hash2",
            RoleId = 1,
            IsActive = true,
            ApprovalStatus = ApprovalStatuses.Approved
        };

        var manager = new User
        {
            Id = 3,
            Username = "manager_john",
            Email = "john.manager@smartparking.test",
            FullName = "John Manager",
            PasswordHash = "hash3",
            RoleId = 2,
            IsActive = true,
            ApprovalStatus = ApprovalStatuses.Approved
        };

        var staff = new User
        {
            Id = 4,
            Username = "staff_alice",
            Email = "alice.staff@smartparking.test",
            FullName = "Alice Staff",
            PasswordHash = "hash4",
            RoleId = 3,
            IsActive = true,
            ApprovalStatus = ApprovalStatuses.Approved
        };

        var regularUser = new User
        {
            Id = 5,
            Username = "user_bob",
            Email = "bob.user@smartparking.test",
            FullName = "Bob Driver",
            PasswordHash = "hash5",
            RoleId = 4,
            IsActive = true,
            ApprovalStatus = ApprovalStatuses.Approved
        };

        var lot = new ParkingLot
        {
            Id = 1,
            Name = "Lot Alpha",
            Address = "123 Main St",
            TotalCapacity = 50,
            IsActive = true
        };

        context.Users.AddRange(admin1, admin2, manager, staff, regularUser);
        context.ParkingLots.Add(lot);
        await context.SaveChangesAsync();

        return (service, context, admin1, admin2, manager, staff, regularUser, lot);
    }

    #region Unit Tests - AdminUserService

    [Fact]
    public async Task GetUsersAsync_WithPaginationAndFilter_ReturnsCorrectUsers()
    {
        var (service, _, _, _, _, _, _, _) = await SetupServiceAsync();

        // Filter by role MANAGER
        var result = await service.GetUsersAsync(new AdminUserFilterDto { Role = Roles.Manager });

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("manager_john", result.Items[0].Username);
        Assert.Equal(Roles.Manager, result.Items[0].Role);
    }

    [Fact]
    public async Task GetUsersAsync_WithSearchQuery_MatchesUsernameAndEmail()
    {
        var (service, _, _, _, _, _, _, _) = await SetupServiceAsync();

        var result = await service.GetUsersAsync(new AdminUserFilterDto { Search = "alice" });

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("staff_alice", result.Items[0].Username);
    }

    [Fact]
    public async Task GetUserByIdAsync_WhenUserExists_ReturnsDetailWithLotAssignments()
    {
        var (service, context, _, _, manager, _, _, lot) = await SetupServiceAsync();

        // Add a lot assignment for the manager
        context.StaffLotAssignments.Add(new StaffLotAssignment
        {
            UserId = manager.Id,
            LotId = lot.Id,
            IsActive = true,
            AssignedBy = 1,
            AssignedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var detail = await service.GetUserByIdAsync(manager.Id);

        Assert.NotNull(detail);
        Assert.Equal(manager.Username, detail.Username);
        Assert.Equal(1, detail.ActiveAssignmentsCount);
        Assert.Single(detail.ActiveAssignments);
        Assert.Equal(lot.Name, detail.ActiveAssignments[0].LotName);
    }

    [Fact]
    public async Task GetUserByIdAsync_WhenUserDoesNotExist_ReturnsNull()
    {
        var (service, _, _, _, _, _, _, _) = await SetupServiceAsync();

        var detail = await service.GetUserByIdAsync(99999);

        Assert.Null(detail);
    }

    [Fact]
    public async Task CreateUserAsync_WithValidStaffRole_CreatesPreApprovedAccountWithAudit()
    {
        var (service, context, admin1, _, _, _, _, _) = await SetupServiceAsync();

        var request = new AdminCreateUserRequestDto
        {
            Username = "new_staff_01",
            Email = "newstaff01@smartparking.test",
            Password = "Password123@",
            FullName = "New Staff Member",
            PhoneNumber = "0987654321",
            Role = Roles.Staff,
            IsActive = true
        };

        var result = await service.CreateUserAsync(request, admin1.Id, "127.0.0.1");

        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("new_staff_01", result.Data.Username);
        Assert.Equal(Roles.Staff, result.Data.Role);
        Assert.True(result.Data.IsActive);
        Assert.True(result.Data.IsEmailVerified);
        Assert.Equal(ApprovalStatuses.Approved, result.Data.ApprovalStatus);

        // Verify audit log
        var audit = context.AuditLogs.FirstOrDefault(a => a.TargetId == result.Data.Id.ToString() && a.Action == "CREATE_USER");
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task CreateUserAsync_WithDuplicateUsername_Returns409Conflict()
    {
        var (service, _, admin1, _, _, _, _, _) = await SetupServiceAsync();

        var request = new AdminCreateUserRequestDto
        {
            Username = "manager_john", // Existing username
            Email = "unique_email@smartparking.test",
            Password = "Password123@",
            FullName = "Duplicate User",
            Role = Roles.Staff
        };

        var result = await service.CreateUserAsync(request, admin1.Id, "127.0.0.1");

        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains("Tên đăng nhập đã được sử dụng", result.Message);
    }

    [Fact]
    public async Task CreateUserAsync_WithMixedCaseUsername_StoresNormalizedLowercaseUsername()
    {
        var (service, context, admin1, _, _, _, _, _) = await SetupServiceAsync();

        var request = new AdminCreateUserRequestDto
        {
            Username = "Staff_Alice_MixedCase",
            Email = "Alice.MixedCase@smartparking.test",
            Password = "Password123@",
            FullName = "Mixed Case Staff",
            Role = Roles.Staff,
            IsActive = true
        };

        var result = await service.CreateUserAsync(request, admin1.Id, "127.0.0.1");

        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal("staff_alice_mixedcase", result.Data!.Username);
        Assert.Equal("alice.mixedcase@smartparking.test", result.Data.Email);

        // Verify entity in database is normalized
        var userInDb = await context.Users.FindAsync(result.Data.Id);
        Assert.NotNull(userInDb);
        Assert.Equal("staff_alice_mixedcase", userInDb.Username);
        Assert.Equal("alice.mixedcase@smartparking.test", userInDb.Email);
    }

    [Fact]
    public async Task CreateUserAsync_WithDuplicateUsernameDifferentCasing_Returns409Conflict()
    {
        var (service, _, admin1, _, _, _, _, _) = await SetupServiceAsync();

        // Existing user has username "manager_john"
        var request = new AdminCreateUserRequestDto
        {
            Username = "MANAGER_JOHN",
            Email = "diff_email_john@smartparking.test",
            Password = "Password123@",
            FullName = "Duplicate Case User",
            Role = Roles.Staff
        };

        var result = await service.CreateUserAsync(request, admin1.Id, "127.0.0.1");

        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains("Tên đăng nhập đã được sử dụng", result.Message);
    }

    [Fact]
    public async Task CreateUserAsync_WithDuplicateEmail_Returns409Conflict()
    {
        var (service, _, admin1, _, _, _, _, _) = await SetupServiceAsync();

        var request = new AdminCreateUserRequestDto
        {
            Username = "brand_new_staff",
            Email = "john.manager@smartparking.test", // Existing manager's email
            Password = "Password123@",
            FullName = "Duplicate Email User",
            Role = Roles.Staff
        };

        var result = await service.CreateUserAsync(request, admin1.Id, "127.0.0.1");

        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains("Địa chỉ email đã được sử dụng", result.Message);
    }

    [Fact]
    public async Task CreateUserAsync_StaffCreatedWithMixedCase_CanLoginWithAnyCasing()
    {
        var (service, context, admin1, _, _, _, _, _) = await SetupServiceAsync();

        var request = new AdminCreateUserRequestDto
        {
            Username = "Staff_Login_Mixed",
            Email = "staff.login.mixed@smartparking.test",
            Password = "Password123@",
            FullName = "Login Mixed Staff",
            Role = Roles.Staff,
            IsActive = true
        };

        var result = await service.CreateUserAsync(request, admin1.Id, "127.0.0.1");
        Assert.True(result.Success);

        var jwtOptions = Microsoft.Extensions.Options.Options.Create(new SmartParking.Application.Common.Options.JwtOptions
        {
            Issuer = "SmartParkingTest",
            Audience = "SmartParkingTestClient",
            SecretKey = "SuperSecureJwtSecretKeyForTesting2026!Min32BytesLongSecret",
            AccessTokenLifetimeMinutes = 15,
            RefreshTokenLifetimeDays = 7
        });
        var secOptions = Microsoft.Extensions.Options.Options.Create(new SmartParking.Application.Common.Options.SecurityOptions
        {
            MaxFailedLoginAttempts = 5,
            LockoutDurationMinutes = 15
        });

        var authService = new AuthService(
            context,
            new PasswordHasherService(),
            new TokenService(jwtOptions),
            null!, null!, null!,
            jwtOptions,
            secOptions,
            Microsoft.Extensions.Options.Options.Create(new SmartParking.Application.Common.Options.RecaptchaOptions()),
            NullLogger<AuthService>.Instance
        );

        // 1. Login with uppercase
        var loginUpper = await authService.LoginAsync(new SmartParking.Application.DTOs.Auth.LoginRequestDto
        {
            Username = "STAFF_LOGIN_MIXED",
            Password = "Password123@"
        }, "TestAgent", "127.0.0.1");
        Assert.True(loginUpper.Succeeded);

        // 2. Login with lowercase
        var loginLower = await authService.LoginAsync(new SmartParking.Application.DTOs.Auth.LoginRequestDto
        {
            Username = "staff_login_mixed",
            Password = "Password123@"
        }, "TestAgent", "127.0.0.1");
        Assert.True(loginLower.Succeeded);

        // 3. Login with mixed case as created
        var loginExact = await authService.LoginAsync(new SmartParking.Application.DTOs.Auth.LoginRequestDto
        {
            Username = "Staff_Login_Mixed",
            Password = "Password123@"
        }, "TestAgent", "127.0.0.1");
        Assert.True(loginExact.Succeeded);
    }

    [Fact]
    public async Task CreateUserAsync_WhenPostgresUniqueViolationOnUsername_MapsTo409Conflict()
    {
        var options = new DbContextOptionsBuilder<SmartParkingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var throwingContext = new ThrowingTestDbContext(options);
        throwingContext.Roles.Add(new Role { Id = 3, Code = Roles.Staff, Name = "Nhân viên vận hành" });
        throwingContext.Users.Add(new User { Id = 1, Username = "admin", Email = "admin@test.com", FullName = "Admin User", RoleId = 1, PasswordHash = "h" });
        await throwingContext.SaveChangesAsync();

        // Simulate PostgreSQL unique constraint exception on uq_users_username
        throwingContext.ExceptionToThrow = new DbUpdateException("Duplicate key", new Exception("23505: duplicate key value violates unique constraint \"uq_users_username\""));

        var service = new AdminUserService(throwingContext, new PasswordHasherService(), NullLogger<AdminUserService>.Instance);

        var request = new AdminCreateUserRequestDto
        {
            Username = "concurrent_staff",
            Email = "concurrent_staff@test.com",
            Password = "Password123@",
            FullName = "Concurrent Staff",
            Role = Roles.Staff
        };

        var result = await service.CreateUserAsync(request, 1, "127.0.0.1");

        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("Tên đăng nhập đã được sử dụng.", result.Message);
    }

    [Fact]
    public async Task CreateUserAsync_WhenPostgresUniqueViolationOnEmail_MapsTo409Conflict()
    {
        var options = new DbContextOptionsBuilder<SmartParkingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var throwingContext = new ThrowingTestDbContext(options);
        throwingContext.Roles.Add(new Role { Id = 3, Code = Roles.Staff, Name = "Nhân viên vận hành" });
        throwingContext.Users.Add(new User { Id = 1, Username = "admin", Email = "admin@test.com", FullName = "Admin User", RoleId = 1, PasswordHash = "h" });
        await throwingContext.SaveChangesAsync();

        // Simulate PostgreSQL unique constraint exception on uq_users_email
        throwingContext.ExceptionToThrow = new DbUpdateException("Duplicate key", new Exception("23505: duplicate key value violates unique constraint \"uq_users_email\""));

        var service = new AdminUserService(throwingContext, new PasswordHasherService(), NullLogger<AdminUserService>.Instance);

        var request = new AdminCreateUserRequestDto
        {
            Username = "concurrent_staff2",
            Email = "concurrent_staff2@test.com",
            Password = "Password123@",
            FullName = "Concurrent Staff 2",
            Role = Roles.Staff
        };

        var result = await service.CreateUserAsync(request, 1, "127.0.0.1");

        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("Địa chỉ email đã được sử dụng.", result.Message);
    }

    [Fact]
    public async Task CreateUserAsync_WhenGenericDatabaseExceptionOccurs_ThrowsWithoutConvertingTo409()
    {
        var options = new DbContextOptionsBuilder<SmartParkingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var throwingContext = new ThrowingTestDbContext(options);
        throwingContext.Roles.Add(new Role { Id = 3, Code = Roles.Staff, Name = "Nhân viên vận hành" });
        throwingContext.Users.Add(new User { Id = 1, Username = "admin", Email = "admin@test.com", FullName = "Admin User", RoleId = 1, PasswordHash = "h" });
        await throwingContext.SaveChangesAsync();

        // Simulate generic non-unique database exception (e.g. connection error)
        throwingContext.ExceptionToThrow = new DbUpdateException("Connection failure", new Exception("Npgsql.NpgsqlException: Connection refused"));

        var service = new AdminUserService(throwingContext, new PasswordHasherService(), NullLogger<AdminUserService>.Instance);

        var request = new AdminCreateUserRequestDto
        {
            Username = "failing_staff",
            Email = "failing_staff@test.com",
            Password = "Password123@",
            FullName = "Failing Staff",
            Role = Roles.Staff
        };

        // Must rethrow and NOT convert to 409
        await Assert.ThrowsAsync<DbUpdateException>(() => service.CreateUserAsync(request, 1, "127.0.0.1"));
    }

    [Fact]
    public async Task CreateUserAsync_WithInvalidRole_Returns400BadRequest()
    {
        var (service, _, admin1, _, _, _, _, _) = await SetupServiceAsync();

        var request = new AdminCreateUserRequestDto
        {
            Username = "invalid_role_user",
            Email = "invalidrole@smartparking.test",
            Password = "Password123@",
            FullName = "Invalid Role",
            Role = "SUPER_DEVELOPER"
        };

        var result = await service.CreateUserAsync(request, admin1.Id, "127.0.0.1");

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("Không hỗ trợ tạo tài khoản", result.Message);
    }

    [Fact]
    public async Task CreateUserAsync_WithAdminRole_Returns400BadRequest()
    {
        var (service, _, admin1, _, _, _, _, _) = await SetupServiceAsync();

        var request = new AdminCreateUserRequestDto
        {
            Username = "attempted_admin",
            Email = "attempted_admin@smartparking.test",
            Password = "Password123@",
            FullName = "Attempted Admin",
            Role = Roles.Admin
        };

        var result = await service.CreateUserAsync(request, admin1.Id, "127.0.0.1");

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("Chỉ cho phép quản trị viên tạo tài khoản nội bộ với vai trò 'STAFF' hoặc 'MANAGER'", result.Message);
    }

    [Fact]
    public async Task CreateUserAsync_WithUserRole_Returns400BadRequest()
    {
        var (service, _, admin1, _, _, _, _, _) = await SetupServiceAsync();

        var request = new AdminCreateUserRequestDto
        {
            Username = "attempted_user",
            Email = "attempted_user@smartparking.test",
            Password = "Password123@",
            FullName = "Attempted User",
            Role = Roles.User
        };

        var result = await service.CreateUserAsync(request, admin1.Id, "127.0.0.1");

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("Chỉ cho phép quản trị viên tạo tài khoản nội bộ với vai trò 'STAFF' hoặc 'MANAGER'", result.Message);
    }

    [Fact]
    public async Task CreateUserAsync_WithManagerRole_Succeeds()
    {
        var (service, _, admin1, _, _, _, _, _) = await SetupServiceAsync();

        var request = new AdminCreateUserRequestDto
        {
            Username = "new_manager_01",
            Email = "new_manager_01@smartparking.test",
            Password = "Password123@",
            FullName = "New Parking Manager",
            Role = Roles.Manager,
            IsActive = true
        };

        var result = await service.CreateUserAsync(request, admin1.Id, "127.0.0.1");

        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal(Roles.Manager, result.Data!.Role);
    }

    [Fact]
    public async Task UpdateUserStatusAsync_ConcurrentLastAdminDeactivation_PreventsZeroActiveAdmins()
    {
        var (service, context, admin1, admin2, _, _, _, _) = await SetupServiceAsync();

        // Both admin1 and admin2 are active.
        // Two concurrent requests attempt to deactivate admin1 and admin2 at the same time.
        var task1 = service.UpdateUserStatusAsync(
            admin1.Id,
            new AdminUpdateStatusRequestDto { IsActive = false },
            999,
            "127.0.0.1");

        var task2 = service.UpdateUserStatusAsync(
            admin2.Id,
            new AdminUpdateStatusRequestDto { IsActive = false },
            999,
            "127.0.0.1");

        var results = await Task.WhenAll(task1, task2);

        // Exactly one must succeed, and one must be rejected to prevent 0 active admins
        var successCount = results.Count(r => r.Success);
        var failureCount = results.Count(r => !r.Success && r.StatusCode == 400);

        Assert.Equal(1, successCount);
        Assert.Equal(1, failureCount);

        // Verify in database that at least one admin remains active
        var activeAdmins = context.Users.Count(u => u.Role.Code == Roles.Admin && u.IsActive);
        Assert.Equal(1, activeAdmins);
    }

    [Fact]
    public async Task UpdateUserStatusAsync_DisablingStaff_RevokesActiveAssignmentsAndTokens()
    {
        var (service, context, admin1, _, _, staff, _, lot) = await SetupServiceAsync();

        // Give staff an active assignment and a refresh token
        var assignment = new StaffLotAssignment
        {
            UserId = staff.Id,
            LotId = lot.Id,
            IsActive = true,
            AssignedBy = admin1.Id,
            AssignedAt = DateTime.UtcNow
        };
        var token = new RefreshToken
        {
            UserId = staff.Id,
            TokenHash = "token_hash",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false
        };
        context.StaffLotAssignments.Add(assignment);
        context.RefreshTokens.Add(token);
        await context.SaveChangesAsync();

        var result = await service.UpdateUserStatusAsync(
            staff.Id,
            new AdminUpdateStatusRequestDto { IsActive = false, Reason = "Tạm ngưng hợp đồng." },
            admin1.Id,
            "127.0.0.1");

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.False(result.Data!.IsActive);

        // Verify assignment is revoked
        var refreshedAssignment = await context.StaffLotAssignments.FindAsync(assignment.Id);
        Assert.False(refreshedAssignment!.IsActive);
        Assert.Equal(admin1.Id, refreshedAssignment.RevokedBy);
        Assert.Contains("Tạm ngưng hợp đồng", refreshedAssignment.RevocationReason);

        // Verify refresh token is revoked
        var refreshedToken = await context.RefreshTokens.FindAsync(token.Id);
        Assert.True(refreshedToken!.IsRevoked);

        // Verify audit log
        var audit = context.AuditLogs.FirstOrDefault(a => a.TargetId == staff.Id.ToString() && a.Action == "UPDATE_USER_STATUS");
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task UpdateUserStatusAsync_WhenAdminDisablesSelf_Returns400BadRequest()
    {
        var (service, _, admin1, _, _, _, _, _) = await SetupServiceAsync();

        var result = await service.UpdateUserStatusAsync(
            admin1.Id,
            new AdminUpdateStatusRequestDto { IsActive = false },
            admin1.Id,
            "127.0.0.1");

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("không thể tự vô hiệu hóa", result.Message);
    }

    [Fact]
    public async Task UpdateUserStatusAsync_WhenDisablingLastActiveAdmin_Returns400BadRequest()
    {
        var (service, context, admin1, admin2, _, _, _, _) = await SetupServiceAsync();

        // Deactivate admin2 first, leaving admin1 as the sole active admin
        admin2.IsActive = false;
        await context.SaveChangesAsync();

        // Attempting to disable admin1 by another actor
        var result = await service.UpdateUserStatusAsync(
            admin1.Id,
            new AdminUpdateStatusRequestDto { IsActive = false },
            999, // Another caller
            "127.0.0.1");

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("cuối cùng của hệ thống", result.Message);
    }

    [Fact]
    public async Task UpdateUserRoleAsync_DemotingStaffToUser_RevokesLotAssignments()
    {
        var (service, context, admin1, _, _, staff, _, lot) = await SetupServiceAsync();

        var assignment = new StaffLotAssignment
        {
            UserId = staff.Id,
            LotId = lot.Id,
            IsActive = true,
            AssignedBy = admin1.Id,
            AssignedAt = DateTime.UtcNow
        };
        context.StaffLotAssignments.Add(assignment);
        await context.SaveChangesAsync();

        var result = await service.UpdateUserRoleAsync(
            staff.Id,
            new AdminUpdateRoleRequestDto { Role = Roles.User, Reason = "Chuyển thành khách hàng tiêu chuẩn." },
            admin1.Id,
            "127.0.0.1");

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(Roles.User, result.Data!.Role);

        // Assignment must be revoked since USER cannot have lot assignments
        var refreshedAssignment = await context.StaffLotAssignments.FindAsync(assignment.Id);
        Assert.False(refreshedAssignment!.IsActive);
        Assert.Contains("USER", refreshedAssignment.RevocationReason);

        // Audit log exists
        var audit = context.AuditLogs.FirstOrDefault(a => a.TargetId == staff.Id.ToString() && a.Action == "UPDATE_USER_ROLE");
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task UpdateUserRoleAsync_WhenAdminDemotesSelf_Returns400BadRequest()
    {
        var (service, _, admin1, _, _, _, _, _) = await SetupServiceAsync();

        var result = await service.UpdateUserRoleAsync(
            admin1.Id,
            new AdminUpdateRoleRequestDto { Role = Roles.Manager },
            admin1.Id,
            "127.0.0.1");

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("không thể tự hạ quyền", result.Message);
    }

    [Fact]
    public async Task UpdateUserRoleAsync_WhenDemotingLastActiveAdmin_Returns400BadRequest()
    {
        var (service, context, admin1, admin2, _, _, _, _) = await SetupServiceAsync();

        // Deactivate admin2 so admin1 is the only active admin
        admin2.IsActive = false;
        await context.SaveChangesAsync();

        var result = await service.UpdateUserRoleAsync(
            admin1.Id,
            new AdminUpdateRoleRequestDto { Role = Roles.Manager },
            999,
            "127.0.0.1");

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("cuối cùng của hệ thống", result.Message);
    }

    #endregion

    #region Integration HTTP Tests - Role Authorization & Endpoints

    private HttpClient CreateClientForRole(string role)
    {
        var client = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var db = scope.ServiceProvider.GetRequiredService<SmartParkingDbContext>();

        var roleId = role switch
        {
            Roles.Admin => 1,
            Roles.Manager => 2,
            Roles.Staff => 3,
            _ => 4
        };

        var userId = 8000L + roleId;
        var user = db.Users.Find(userId);
        if (user == null)
        {
            user = new User
            {
                Id = userId,
                Username = $"admin_test_{role.ToLower()}",
                Email = $"{role.ToLower()}_mgmt@smartparking.test",
                FullName = $"Admin Test {role}",
                PasswordHash = "dummy_hash",
                RoleId = roleId,
                IsActive = true,
                ApprovalStatus = ApprovalStatuses.Approved
            };
            db.Users.Add(user);
            db.SaveChanges();
        }

        var token = tokenService.GenerateAccessToken(user, role);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task AdminEndpoints_WithoutToken_Return401Unauthorized()
    {
        var unauthenticatedClient = _factory.CreateClient();

        var response = await unauthenticatedClient.GetAsync("/api/v1/admin/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Staff)]
    [InlineData(Roles.User)]
    public async Task AdminEndpoints_CalledByNonAdminRoles_Return403Forbidden(string role)
    {
        var nonAdminClient = CreateClientForRole(role);

        var getResponse = await nonAdminClient.GetAsync("/api/v1/admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, getResponse.StatusCode);

        var postResponse = await nonAdminClient.PostAsJsonAsync("/api/v1/admin/users", new AdminCreateUserRequestDto
        {
            Username = "some_user",
            Email = "user@test.com",
            Password = "Password123@",
            FullName = "Some User",
            Role = Roles.Staff
        });
        Assert.Equal(HttpStatusCode.Forbidden, postResponse.StatusCode);

        var patchStatusResponse = await nonAdminClient.PatchAsJsonAsync("/api/v1/admin/users/1/status", new AdminUpdateStatusRequestDto
        {
            IsActive = false
        });
        Assert.Equal(HttpStatusCode.Forbidden, patchStatusResponse.StatusCode);

        var patchRoleResponse = await nonAdminClient.PatchAsJsonAsync("/api/v1/admin/users/1/role", new AdminUpdateRoleRequestDto
        {
            Role = Roles.User
        });
        Assert.Equal(HttpStatusCode.Forbidden, patchRoleResponse.StatusCode);
    }

    [Fact]
    public async Task AdminEndpoints_CalledByAdmin_Succeed()
    {
        var adminClient = CreateClientForRole(Roles.Admin);

        // 1. GET /api/v1/admin/users
        var getListResponse = await adminClient.GetAsync("/api/v1/admin/users?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, getListResponse.StatusCode);

        // 2. POST /api/v1/admin/users (Create a new Staff)
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..6];
        var createRequest = new AdminCreateUserRequestDto
        {
            Username = $"staff_{uniqueSuffix}",
            Email = $"staff_{uniqueSuffix}@smartparking.test",
            Password = "Password123@",
            FullName = "Integration Staff",
            PhoneNumber = "0901234567",
            Role = Roles.Staff,
            IsActive = true
        };

        var createResponse = await adminClient.PostAsJsonAsync("/api/v1/admin/users", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdJson = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var newUserId = createdJson.GetProperty("data").GetProperty("id").GetInt64();

        // 3. GET /api/v1/admin/users/{id}
        var getDetailResponse = await adminClient.GetAsync($"/api/v1/admin/users/{newUserId}");
        Assert.Equal(HttpStatusCode.OK, getDetailResponse.StatusCode);

        // 4. PATCH /api/v1/admin/users/{id}/status (Disable account)
        var patchStatusResponse = await adminClient.PatchAsJsonAsync($"/api/v1/admin/users/{newUserId}/status", new AdminUpdateStatusRequestDto
        {
            IsActive = false,
            Reason = "Vô hiệu hóa thử nghiệm"
        });
        Assert.Equal(HttpStatusCode.OK, patchStatusResponse.StatusCode);

        // 5. PATCH /api/v1/admin/users/{id}/role (Elevate to Manager)
        var patchRoleResponse = await adminClient.PatchAsJsonAsync($"/api/v1/admin/users/{newUserId}/role", new AdminUpdateRoleRequestDto
        {
            Role = Roles.Manager,
            Reason = "Thăng cấp lên quản lý"
        });
        Assert.Equal(HttpStatusCode.OK, patchRoleResponse.StatusCode);
    }

    #endregion
}

public class ThrowingTestDbContext : TestSmartParkingDbContext
{
    public Exception? ExceptionToThrow { get; set; }

    public ThrowingTestDbContext(DbContextOptions<SmartParkingDbContext> options) : base(options) { }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (ExceptionToThrow != null)
        {
            throw ExceptionToThrow;
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
