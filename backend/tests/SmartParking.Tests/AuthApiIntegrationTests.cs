using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SmartParking.Application.DTOs.Auth;
using Xunit;

namespace SmartParking.Tests;

public class AuthApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AuthApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/api/v1/auth/register")]
    [InlineData("/api/v1/auth/verify-email")]
    [InlineData("/api/v1/auth/resend-verification")]
    public async Task RestoredAccountCreationEndpoint_WithEmptyPayload_ReturnsBadRequest(string path)
    {
        var response = await _client.PostAsJsonAsync(path, new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithEmptyCredentials_ReturnsBadRequest()
    {
        // Arrange
        var request = new LoginRequestDto();

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUserProfile_WithoutToken_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/users/me");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCurrentUserProfile_WithoutToken_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.PutAsJsonAsync("/api/v1/users/me", new UpdateProfileRequestDto
        {
            FullName = "New Name"
        });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SwaggerJson_ContainsAccountsEndpoints()
    {
        // Act
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Contains("/api/v1/auth/register", content);
        Assert.Contains("/api/v1/auth/login", content);
        Assert.Contains("/api/v1/auth/verify-email", content);
        Assert.Contains("/api/v1/auth/resend-verification", content);
        Assert.Contains("/api/v1/auth/refresh", content);
        Assert.Contains("/api/v1/auth/logout", content);
        Assert.Contains("/api/v1/users/me", content);
    }
}
