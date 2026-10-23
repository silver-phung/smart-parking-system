using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.Application.DTOs.Auth;
using SmartParking.Domain.Constants;
using SmartParking.Domain.Entities;
using SmartParking.Infrastructure.Services;
using Xunit;

namespace SmartParking.Tests;

public class UserServiceTests
{
    [Fact]
    public async Task GetUserProfileAsync_WithExistingUser_ReturnsProfileWithoutSensitiveFields()
    {
        // Arrange
        using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var customerRole = context.Roles.First(r => r.Code == Roles.Customer);

        var user = new User
        {
            Id = 10,
            Username = "profileuser",
            Email = "profile@example.com",
            PasswordHash = "hashed_secret_password",
            FullName = "Profile User",
            PhoneNumber = "0912345678",
            RoleId = customerRole.Id,
            Role = customerRole,
            IsEmailVerified = true,
            ApprovalStatus = ApprovalStatuses.Approved,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var userService = new UserService(context, NullLogger<UserService>.Instance);

        // Act
        var profile = await userService.GetUserProfileAsync(10);

        // Assert
        Assert.NotNull(profile);
        Assert.Equal("profileuser", profile.Username);
        Assert.Equal("profile@example.com", profile.Email);
        Assert.Equal("Profile User", profile.FullName);
        Assert.Equal("0912345678", profile.PhoneNumber);
        Assert.Equal(Roles.Customer, profile.Role);
        Assert.True(profile.IsEmailVerified);
        Assert.Equal(ApprovalStatuses.Approved, profile.ApprovalStatus);
    }

    [Fact]
    public async Task UpdateUserProfileAsync_UpdatesOnlyAllowedFields()
    {
        // Arrange
        using var context = await TestDbHelper.CreateInMemoryDbContextAsync();
        var customerRole = context.Roles.First(r => r.Code == Roles.Customer);

        var user = new User
        {
            Id = 20,
            Username = "original_username",
            Email = "original@example.com",
            PasswordHash = "original_hash",
            FullName = "Original Name",
            PhoneNumber = "0900000000",
            RoleId = customerRole.Id,
            Role = customerRole,
            IsEmailVerified = true,
            ApprovalStatus = ApprovalStatuses.Approved
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var userService = new UserService(context, NullLogger<UserService>.Instance);

        // Act
        var updateRequest = new UpdateProfileRequestDto
        {
            FullName = "Updated Name",
            PhoneNumber = "0988888888"
        };
        var updatedProfile = await userService.UpdateUserProfileAsync(20, updateRequest);

        // Assert
        Assert.NotNull(updatedProfile);
        Assert.Equal("Updated Name", updatedProfile.FullName);
        Assert.Equal("0988888888", updatedProfile.PhoneNumber);

        // Verify in DB that sensitive/immutable fields were NOT changed
        var dbUser = context.Users.First(u => u.Id == 20);
        Assert.Equal("original_username", dbUser.Username);
        Assert.Equal("original@example.com", dbUser.Email);
        Assert.Equal("original_hash", dbUser.PasswordHash);
        Assert.Equal(customerRole.Id, dbUser.RoleId);
        Assert.Equal(ApprovalStatuses.Approved, dbUser.ApprovalStatus);
    }
}
