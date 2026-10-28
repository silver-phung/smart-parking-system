using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartParking.Application.Common.Options;
using SmartParking.Domain.Constants;
using SmartParking.Infrastructure.Services;
using Xunit;

namespace SmartParking.Tests;

public class AdminBootstrapServiceTests
{
    private readonly PasswordHasherService _hasher = new();

    [Fact]
    public async Task BootstrapAdminAsync_WhenDisabled_DoesNotCreateUserEvenIfCredentialsProvided()
    {
        // Arrange
        using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var options = Options.Create(new AdminBootstrapOptions
        {
            Enabled = false,
            Username = "sysadmin",
            Password = "SuperAdminPassword123!",
            Email = "admin@smartparking.vn"
        });

        var service = new AdminBootstrapService(context, _hasher, options, NullLogger<AdminBootstrapService>.Instance);

        // Act
        await service.BootstrapAdminAsync();

        // Assert
        Assert.Empty(context.Users);
    }

    [Fact]
    public async Task BootstrapAdminAsync_WhenCredentialsMissing_DoesNotCreateUser()
    {
        // Arrange
        using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var options = Options.Create(new AdminBootstrapOptions
        {
            Enabled = true,
            Username = "",
            Password = "",
            Email = ""
        });

        var service = new AdminBootstrapService(context, _hasher, options, NullLogger<AdminBootstrapService>.Instance);

        // Act
        await service.BootstrapAdminAsync();

        // Assert
        Assert.Empty(context.Users);
    }

    [Fact]
    public async Task BootstrapAdminAsync_WhenNoAdminExists_SeedsSingleAdminAccount()
    {
        // Arrange
        using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var options = Options.Create(new AdminBootstrapOptions
        {
            Enabled = true,
            Username = "sysadmin",
            Password = "SuperAdminPassword123!",
            Email = "admin@smartparking.vn",
            FullName = "Hệ thống Quản trị",
            PhoneNumber = "0987654321"
        });

        var service = new AdminBootstrapService(context, _hasher, options, NullLogger<AdminBootstrapService>.Instance);

        // Act
        await service.BootstrapAdminAsync();

        // Assert
        var admin = await context.Users.Include(u => u.Role).FirstOrDefaultAsync();
        Assert.NotNull(admin);
        Assert.Equal("sysadmin", admin.Username);
        Assert.Equal("admin@smartparking.vn", admin.Email);
        Assert.Equal(Roles.Admin, admin.Role.Code);
        Assert.True(admin.IsEmailVerified);
        Assert.True(admin.IsActive);
        Assert.Equal(ApprovalStatuses.Approved, admin.ApprovalStatus);
        Assert.True(_hasher.VerifyPassword(admin, admin.PasswordHash, "SuperAdminPassword123!"));
    }

    [Fact]
    public async Task BootstrapAdminAsync_WhenAdminAlreadyExists_IsIdempotent()
    {
        // Arrange
        using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var options = Options.Create(new AdminBootstrapOptions
        {
            Enabled = true,
            Username = "sysadmin",
            Password = "SuperAdminPassword123!",
            Email = "admin@smartparking.vn"
        });

        var service = new AdminBootstrapService(context, _hasher, options, NullLogger<AdminBootstrapService>.Instance);

        // Act: Run twice
        await service.BootstrapAdminAsync();
        await service.BootstrapAdminAsync();

        // Assert: Exactly one admin user created
        Assert.Equal(1, await context.Users.CountAsync());
    }
}
