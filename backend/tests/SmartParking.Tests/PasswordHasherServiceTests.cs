using SmartParking.Domain.Entities;
using SmartParking.Infrastructure.Services;
using Xunit;

namespace SmartParking.Tests;

public class PasswordHasherServiceTests
{
    private readonly PasswordHasherService _hasher;

    public PasswordHasherServiceTests()
    {
        _hasher = new PasswordHasherService();
    }

    [Fact]
    public void HashPassword_ShouldReturnValidHashNotPlaintext()
    {
        // Arrange
        var user = new User { Id = 1, Username = "testuser" };
        var password = "SecurePassword123!";

        // Act
        var hash = _hasher.HashPassword(user, password);

        // Assert
        Assert.NotNull(hash);
        Assert.NotEqual(password, hash);
        Assert.True(hash.Length > 20);
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ShouldReturnTrue()
    {
        // Arrange
        var user = new User { Id = 1, Username = "testuser" };
        var password = "SecurePassword123!";
        var hash = _hasher.HashPassword(user, password);

        // Act
        var isValid = _hasher.VerifyPassword(user, hash, password);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void VerifyPassword_WithWrongPassword_ShouldReturnFalse()
    {
        // Arrange
        var user = new User { Id = 1, Username = "testuser" };
        var password = "SecurePassword123!";
        var hash = _hasher.HashPassword(user, password);

        // Act
        var isValid = _hasher.VerifyPassword(user, hash, "WrongPassword456!");

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void DummyVerify_ShouldExecuteWithoutExceptions()
    {
        // Act & Assert
        var exception = Record.Exception(() => _hasher.DummyVerify());
        Assert.Null(exception);
    }
}
