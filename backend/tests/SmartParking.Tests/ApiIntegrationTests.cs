using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SmartParking.Tests;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHealth_ReturnsOkStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetRoot_ReturnsFrontendHome()
    {
        var response = await _client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Smart-Parking", content);
        Assert.Contains("/app/assets/", content);
    }

    [Theory]
    [InlineData("/app/")]
    [InlineData("/app/login")]
    [InlineData("/app/register")]
    public async Task GetFrontendRoutes_ReturnsSinglePageApp(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("id=\"root\"", content);
    }

    [Fact]
    public async Task GetDashboardPage_ReturnsOriginalDashboard()
    {
        var response = await _client.GetAsync("/dashboard.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Tổng quan bãi đỗ xe", content);
    }

    [Fact]
    public async Task GetSystemInfo_ReturnsExpectedPayload()
    {
        // Act
        var response = await _client.GetAsync("/api/system/info");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Smart Parking API", content);
    }

    [Fact]
    public async Task GetSystemHealth_ReturnsHealthy()
    {
        // Act
        var response = await _client.GetAsync("/api/system/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", content);
    }

    [Fact]
    public async Task GetSwaggerJson_ReturnsValidOpenApiSpecification()
    {
        // Act
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Smart Parking API", content);
    }
}
