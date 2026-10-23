using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using SmartParking.Application.Common.Options;
using SmartParking.Domain.Constants;
using SmartParking.Domain.Entities;
using SmartParking.Infrastructure.Services;
using Xunit;

namespace SmartParking.Tests;

public class TokenServiceTests
{
    private readonly TokenService _tokenService;

    public TokenServiceTests()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            SecretKey = "TestSuperSecretKeyForJWTTokenGeneration1234567890!MustBeLonger",
            AccessTokenLifetimeMinutes = 15,
            RefreshTokenLifetimeDays = 7
        });
        _tokenService = new TokenService(options);
    }

    [Fact]
    public void GenerateAccessToken_ShouldProduceValidJwtWithExpectedClaims()
    {
        // Arrange
        var user = new User
        {
            Id = 42,
            Username = "johndoe",
            Email = "john@example.com"
        };

        // Act
        var token = _tokenService.GenerateAccessToken(user, Roles.Customer);

        // Assert
        Assert.NotNull(token);
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal("TestIssuer", jwt.Issuer);
        Assert.Contains("TestAudience", jwt.Audiences);

        var subClaim = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
        var roleClaim = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;

        Assert.Equal("42", subClaim);
        Assert.Equal(Roles.Customer, roleClaim);
    }

    [Fact]
    public void GenerateRefreshToken_ShouldReturnRawTokenAndSha256Hash()
    {
        // Act
        var (rawToken, tokenHash) = _tokenService.GenerateRefreshToken();

        // Assert
        Assert.NotNull(rawToken);
        Assert.NotNull(tokenHash);
        Assert.NotEqual(rawToken, tokenHash);
        Assert.Equal(64, tokenHash.Length); // 64 hex characters for SHA-256

        // Hash verification
        var recomputedHash = _tokenService.HashToken(rawToken);
        Assert.Equal(tokenHash, recomputedHash);
    }
}
