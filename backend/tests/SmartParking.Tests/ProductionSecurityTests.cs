using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartParking.Application.Common.Options;
using SmartParking.Infrastructure.Services;
using Xunit;

namespace SmartParking.Tests;

public class FakeHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Production;
    public string ApplicationName { get; set; } = "SmartParking.Api";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
}

public class ProductionSecurityTests
{
    [Fact]
    public void TokenService_WithEmptySecretKey_ThrowsInvalidOperationException()
    {
        var options = Options.Create(new JwtOptions { SecretKey = "" });
        var ex = Assert.Throws<InvalidOperationException>(() => new TokenService(options));
        Assert.Contains("Jwt:SecretKey is missing or less than 32 bytes", ex.Message);
    }

    [Fact]
    public void TokenService_WithShortSecretKey_ThrowsInvalidOperationException()
    {
        // 16 bytes is shorter than the required 32 bytes (256 bits)
        var options = Options.Create(new JwtOptions { SecretKey = "short_key_16_b!" });
        var ex = Assert.Throws<InvalidOperationException>(() => new TokenService(options));
        Assert.Contains("Jwt:SecretKey is missing or less than 32 bytes", ex.Message);
    }

    [Fact]
    public void OtpService_WithEmptySecretKey_ThrowsInvalidOperationException()
    {
        var options = Options.Create(new SecurityOptions { OtpSecretKey = "" });
        var ex = Assert.Throws<InvalidOperationException>(() => new OtpService(options));
        Assert.Contains("Security:OtpSecretKey is missing or less than 32 bytes", ex.Message);
    }

    [Fact]
    public void OtpService_WithShortSecretKey_ThrowsInvalidOperationException()
    {
        var options = Options.Create(new SecurityOptions { OtpSecretKey = "short_key_16_b!" });
        var ex = Assert.Throws<InvalidOperationException>(() => new OtpService(options));
        Assert.Contains("Security:OtpSecretKey is missing or less than 32 bytes", ex.Message);
    }

    [Fact]
    public async Task RecaptchaService_InProduction_RejectsBypassEvenIfDisabledInConfig()
    {
        // Arrange: Production environment, but config tries to disable reCAPTCHA
        var prodEnv = new FakeHostEnvironment { EnvironmentName = Environments.Production };
        var options = Options.Create(new RecaptchaOptions
        {
            Enabled = false,
            SecretKey = ""
        });
        using var httpClient = new HttpClient();
        var service = new RecaptchaService(options, httpClient, prodEnv, NullLogger<RecaptchaService>.Instance);

        // Act
        var result = await service.VerifyAsync("some-token");

        // Assert: CRITICAL FAIL-CLOSED: Can NEVER bypass in Production
        Assert.False(result);
    }

    [Fact]
    public async Task RecaptchaService_WhenEnabled_RejectsEmptySecretKey()
    {
        var devEnv = new FakeHostEnvironment { EnvironmentName = Environments.Development };
        var options = Options.Create(new RecaptchaOptions
        {
            Enabled = true,
            SecretKey = ""
        });
        using var httpClient = new HttpClient();
        var service = new RecaptchaService(options, httpClient, devEnv, NullLogger<RecaptchaService>.Instance);

        // Act
        var result = await service.VerifyAsync("some-token");

        // Assert: Fail-closed when secret is missing
        Assert.False(result);
    }

    [Fact]
    public async Task RecaptchaService_WhenEnabled_RejectsEmptyToken()
    {
        var devEnv = new FakeHostEnvironment { EnvironmentName = Environments.Development };
        var options = Options.Create(new RecaptchaOptions
        {
            Enabled = true,
            SecretKey = "valid-secret-key-6LeIxAcTAAAAAGG-vFI1TnRWxMZNFuojJ4WifJWe"
        });
        using var httpClient = new HttpClient();
        var service = new RecaptchaService(options, httpClient, devEnv, NullLogger<RecaptchaService>.Instance);

        // Act
        var result = await service.VerifyAsync("");

        // Assert: Empty token must be rejected
        Assert.False(result);
    }
}
