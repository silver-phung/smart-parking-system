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
        Assert.Contains("/api/v1/parking-lots", content);
        Assert.Contains("/api/v1/parking-floors", content);
        Assert.Contains("/api/v1/parking-slots", content);
    }

    [Theory]
    [InlineData("http://localhost:5173")]
    [InlineData("http://localhost:3000")]
    public async Task OptionsPreflight_WithAllowedOrigin_ReturnsCorsHeaders(string allowedOrigin)
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/register");
        request.Headers.Add("Origin", allowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.True(response.StatusCode == HttpStatusCode.NoContent || response.StatusCode == HttpStatusCode.OK);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Equal(allowedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").FirstOrDefault());
        Assert.True(response.Headers.Contains("Access-Control-Allow-Methods"));
        Assert.True(response.Headers.Contains("Access-Control-Allow-Headers"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task OptionsPreflight_WithDisallowedOrigin_DoesNotReturnCorsHeaders()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/register");
        request.Headers.Add("Origin", "http://unauthorized-malicious-origin.com");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task GetEndpoint_WithAllowedOrigin_ReturnsAccessControlAllowOriginHeader()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/system/info");
        request.Headers.Add("Origin", "http://localhost:5173");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Equal("http://localhost:5173", response.Headers.GetValues("Access-Control-Allow-Origin").FirstOrDefault());
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task GetEndpoint_WithDisallowedOrigin_DoesNotReturnAccessControlAllowOriginHeader()
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/system/info");
        request.Headers.Add("Origin", "http://unauthorized-malicious-origin.com");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task GetParkingLots_Anonymous_Returns200Ok()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/parking-lots");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Lấy danh sách bãi đỗ xe thành công", content);
    }

    [Fact]
    public async Task PostParkingLot_WithoutAuth_Returns401Unauthorized()
    {
        // Arrange
        var body = new StringContent("{\"name\":\"Test Lot\"}", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/parking-lots", body);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PatchParkingSlotStatus_WithoutAuth_Returns401Unauthorized()
    {
        // Arrange
        var body = new StringContent("{\"physicalStatus\":\"MAINTENANCE\"}", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PatchAsync("/api/v1/parking-slots/1/status", body);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
