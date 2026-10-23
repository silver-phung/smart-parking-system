using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartParking.Application.Common.Options;
using SmartParking.Infrastructure.Services;
using Xunit;

namespace SmartParking.Tests;

public class EmailServiceTests
{
    [Fact]
    public async Task SendVerificationOtpAsync_InDevelopment_WhenProviderIsMock_ReturnsTrue()
    {
        // Arrange
        var devEnv = new FakeHostEnvironment { EnvironmentName = Environments.Development };
        var options = Options.Create(new EmailOptions
        {
            Provider = "Mock",
            SenderEmail = "noreply@smartparking.vn"
        });
        var service = new EmailService(options, devEnv, NullLogger<EmailService>.Instance);

        // Act
        var result = await service.SendVerificationOtpAsync("test@example.com", "123456");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task SendVerificationOtpAsync_InProduction_WhenProviderIsMock_RejectsAndReturnsFalse()
    {
        // Arrange: Critical security test: Mock MUST fail in Production
        var prodEnv = new FakeHostEnvironment { EnvironmentName = Environments.Production };
        var options = Options.Create(new EmailOptions
        {
            Provider = "Mock",
            SenderEmail = "noreply@smartparking.vn"
        });
        var service = new EmailService(options, prodEnv, NullLogger<EmailService>.Instance);

        // Act
        var result = await service.SendVerificationOtpAsync("test@example.com", "123456");

        // Assert: Fail-closed in Production
        Assert.False(result);
    }

    [Fact]
    public async Task SendVerificationOtpAsync_InProduction_WhenSmtpCredentialsMissing_ReturnsFalse()
    {
        // Arrange: Production environment requires explicit UserName and Password
        var prodEnv = new FakeHostEnvironment { EnvironmentName = Environments.Production };
        var options = Options.Create(new EmailOptions
        {
            Provider = "Smtp",
            Host = "smtp.sendgrid.net",
            SenderEmail = "noreply@smartparking.vn",
            UserName = "",
            Password = ""
        });
        var service = new EmailService(options, prodEnv, NullLogger<EmailService>.Instance);

        // Act
        var result = await service.SendVerificationOtpAsync("test@example.com", "123456");

        // Assert: Fail-closed without credentials
        Assert.False(result);
    }

    [Fact]
    public async Task SendVerificationOtpAsync_WhenHostOrSenderEmailMissing_ReturnsFalseGracefully()
    {
        // Arrange
        var devEnv = new FakeHostEnvironment { EnvironmentName = Environments.Development };
        var options = Options.Create(new EmailOptions
        {
            Provider = "Smtp",
            Host = "",
            SenderEmail = ""
        });
        var service = new EmailService(options, devEnv, NullLogger<EmailService>.Instance);

        // Act
        var result = await service.SendVerificationOtpAsync("test@example.com", "123456");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task SendVerificationOtpAsync_WhenSmtpConnectionFails_ReturnsFalseWithoutThrowing()
    {
        // Arrange
        var devEnv = new FakeHostEnvironment { EnvironmentName = Environments.Development };
        var options = Options.Create(new EmailOptions
        {
            Provider = "Smtp",
            Host = "127.0.0.1",
            Port = 65534, // unreachable port
            SenderEmail = "noreply@smartparking.vn",
            TimeoutSeconds = 1
        });
        var service = new EmailService(options, devEnv, NullLogger<EmailService>.Instance);

        // Act
        var result = await service.SendVerificationOtpAsync("test@example.com", "123456");

        // Assert
        Assert.False(result);
    }
}
