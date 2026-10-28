using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SmartParking.Api.Controllers;
using Xunit;

namespace SmartParking.Tests;

public class GeminiControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public GeminiControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AnalyzeVehicle_MissingImageUrl_ReturnsBadRequest()
    {
        var response = await _client.PostAsync("/api/gemini/analyze?imageUrl=", null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnalyzeVehicle_InvalidImageUrl_ReturnsBadRequest()
    {
        var response = await _client.PostAsync("/api/gemini/analyze?imageUrl=not-a-valid-url", null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnalyzeVehicle_ValidUrl_WithNoOpAnalyzer_ReturnsUnprocessableOrOk()
    {
        // Khi không có API key trong dev/test, NoOpGeminiVehicleAnalyzer được inject, trả về IsSuccess = false (hoặc ErrorMessage) -> 422 UnprocessableEntity
        var response = await _client.PostAsync("/api/gemini/analyze?imageUrl=https://example.com/car.jpg", null);
        
        Assert.True(response.StatusCode == HttpStatusCode.UnprocessableEntity || response.StatusCode == HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GeminiAnalyzeResponse>();
        Assert.NotNull(body);
        Assert.Equal("https://example.com/car.jpg", body.ImageUrl);
    }
}
