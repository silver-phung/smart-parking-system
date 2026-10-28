using Microsoft.Extensions.Options;
using SmartParking.Application.Common.Options;
using SmartParking.Infrastructure.Services;
using Xunit;

namespace SmartParking.Tests;

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
}
