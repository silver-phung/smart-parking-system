using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Domain.Constants;
using SmartParking.Infrastructure.Data;

namespace SmartParking.Tests;

public class AuthFlowApiTests
{
    [Fact]
    public async Task ExistingAccount_CanLogin_GetProfile_AndLogout()
    {
        using var factory = new CustomWebApplicationFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SmartParkingDbContext>();
            var user = context.Users.Single(user => user.Id == 9004);
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();
            user.PasswordHash = hasher.HashPassword(user, "Existing-account-test-123!");
            user.IsEmailVerified = true;
            await context.SaveChangesAsync();
        }
        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = "test_user", password = "Existing-account-test-123!"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(Roles.User, auth.GetProperty("userProfile").GetProperty("role").GetString());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", auth.GetProperty("accessToken").GetString());
        var profile = await client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        var userProfile = await profile.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("test_user", userProfile.GetProperty("data").GetProperty("username").GetString());
        var refreshToken = auth.GetProperty("refreshToken").GetString();
        var logout = await client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken });
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        var refresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }
}
