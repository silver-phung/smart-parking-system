using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Domain.Constants;
using SmartParking.Infrastructure.Data;

namespace SmartParking.Tests;

public class ImageApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    public ImageApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task DatabaseImage_RequiresAuthenticationAndOwnerOrAdmin()
    {
        var owner = CreateClient(9003, Roles.Staff);
        var other = CreateClient(9004, Roles.User);
        var admin = CreateClient(9001, Roles.Admin);
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "test.png");
        var upload = await owner.PostAsync("/api/v1/images", form);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var json = await upload.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var url = json.GetProperty("data").GetProperty("url").GetString()!;
        var id = json.GetProperty("data").GetProperty("id").GetInt64();
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(url)).StatusCode);
        var response = await owner.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType!.MediaType);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal(png, await response.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/v1/images/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(url)).StatusCode);
    }

    private HttpClient CreateClient(long userId, string role)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartParkingDbContext>();
        var user = db.Users.Find(userId)!;
        var token = scope.ServiceProvider.GetRequiredService<ITokenService>().GenerateAccessToken(user, role);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
