using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartParking.Application.Common.Options;
using SmartParking.Infrastructure.Services;
using Xunit;

namespace SmartParking.Tests;

public class EmailServiceTests
{
    [Fact]
    public async Task SendVerificationOtpAsync_WhenProviderIsMock_ReturnsTrue()
    {
        // Arrange
        var options = Options.Create(new EmailOptions
        {
            Provider = "Mock",
            SenderEmail = "noreply@smartparking.vn"
        });
        var service = new EmailService(options, NullLogger<EmailService>.Instance);

        // Act
        var result = await service.SendVerificationOtpAsync("test@example.com", "123456");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task SendVerificationOtpAsync_WhenHostOrSenderEmailMissing_ReturnsFalseGracefully()
    {
        // Arrange
        var options = Options.Create(new EmailOptions
        {
            Provider = "Smtp",
            Host = "",
            SenderEmail = ""
        });
        var service = new EmailService(options, NullLogger<EmailService>.Instance);

        // Act
        var result = await service.SendVerificationOtpAsync("test@example.com", "123456");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task SendVerificationOtpAsync_WhenSmtpConnectionFails_ReturnsFalseWithoutThrowing()
    {
        // Arrange
        var options = Options.Create(new EmailOptions
        {
            Provider = "Smtp",
            Host = "127.0.0.1",
            Port = 65534, // unreachable port
            SenderEmail = "noreply@smartparking.vn",
            TimeoutSeconds = 1
        });
        var service = new EmailService(options, NullLogger<EmailService>.Instance);

        // Act
        var result = await service.SendVerificationOtpAsync("test@example.com", "123456");

        // Assert
        Assert.False(result);
    }
}
