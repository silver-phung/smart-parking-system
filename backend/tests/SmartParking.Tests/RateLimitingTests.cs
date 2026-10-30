using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SmartParking.Application.DTOs.Auth;
using Xunit;

namespace SmartParking.Tests;

public class RateLimitingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RateLimitingTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RateLimiting_LoginEndpoint_Returns429WhenLimitExceeded()
    {
        // Arrange: Create a dedicated client representing an IP
        var client = _factory.CreateClient();

        HttpResponseMessage? lastResponse = null;

        // Login policy limit is 10 requests per minute
        for (int i = 0; i < 12; i++)
        {
            lastResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto
            {
                Username = $"rate_test_{i}",
                Password = "DummyPassword123!"
            });
        }

        // Assert: Requests after the permit limit must be rejected with 429 Too Many Requests
        Assert.NotNull(lastResponse);
        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse.StatusCode);

        var content = await lastResponse.Content.ReadAsStringAsync();
        Assert.Contains("Bạn đã gửi quá nhiều yêu cầu", content);
    }
}
