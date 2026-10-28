using Microsoft.Extensions.Options;
using SmartParking.Application.Common.Options;
using SmartParking.Domain.Constants;
using SmartParking.Infrastructure.Services;
using Xunit;

namespace SmartParking.Tests;

public class OtpServiceTests
{
    private readonly OtpService _otpService;

    public OtpServiceTests()
    {
        var options = Options.Create(new SecurityOptions
        {
            OtpSecretKey = "TestOtpSecretKeyForHMACSHA256Generation1234567890!"
        });
        _otpService = new OtpService(options);
    }

    [Fact]
    public void GenerateOtpCode_ShouldReturnSixDigitNumericString()
    {
        // Act
        var code = _otpService.GenerateOtpCode();

        // Assert
        Assert.NotNull(code);
        Assert.Equal(6, code.Length);
        Assert.True(int.TryParse(code, out _));
    }

    [Fact]
    public void VerifyOtpHash_WithCorrectInputs_ShouldReturnTrue()
    {
        // Arrange
        long userId = 100;
        string tokenType = TokenTypes.EmailVerification;
        string otpCode = "123456";

        // Act
        var hash = _otpService.ComputeOtpHash(userId, tokenType, otpCode);
        var isValid = _otpService.VerifyOtpHash(userId, tokenType, otpCode, hash);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void VerifyOtpHash_WithWrongCode_ShouldReturnFalse()
    {
        // Arrange
        long userId = 100;
        string tokenType = TokenTypes.EmailVerification;
        string otpCode = "123456";
        var hash = _otpService.ComputeOtpHash(userId, tokenType, otpCode);

        // Act
        var isValid = _otpService.VerifyOtpHash(userId, tokenType, "654321", hash);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void VerifyOtpHash_WithWrongUserId_ShouldReturnFalse()
    {
        // Arrange
        long userId = 100;
        string tokenType = TokenTypes.EmailVerification;
        string otpCode = "123456";
        var hash = _otpService.ComputeOtpHash(userId, tokenType, otpCode);

        // Act
        var isValid = _otpService.VerifyOtpHash(200, tokenType, otpCode, hash);

        // Assert
        Assert.False(isValid);
    }
}
