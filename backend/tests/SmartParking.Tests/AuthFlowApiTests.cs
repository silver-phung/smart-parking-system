using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Domain.Constants;

namespace SmartParking.Tests;

public class AuthFlowApiTests
{
    [Fact]
    public async Task Register_VerifyEmail_Login_AndGetProfile_UsesRealAuthContract()
    {
        var emailService = new FakeEmailService();
        using var baseFactory = new CustomWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Recaptcha:Enabled"] = "false",
                    ["AdminBootstrap:Enabled"] = "false"
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEmailService>();
                services.AddSingleton<IEmailService>(emailService);
            });
        });
        using var client = factory.CreateClient();
        const string username = "auth_flow_user";
        const string email = "auth-flow@smartparking.test";
        const string password = "Auth-flow-test-123!";

        var registration = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            username,
            email,
            password,
            fullName = "Khách kiểm thử"
        });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var registered = await registration.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(username, registered.GetProperty("data").GetProperty("username").GetString());
        Assert.False(registered.GetProperty("data").GetProperty("isEmailVerified").GetBoolean());
        var sentEmail = Assert.Single(emailService.SentEmails);
        Assert.Equal(email, sentEmail.ToEmail);

        var unverifiedLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password });
        Assert.Equal(HttpStatusCode.Forbidden, unverifiedLogin.StatusCode);

        var verification = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new
        {
            email,
            code = sentEmail.OtpCode
        });
        Assert.Equal(HttpStatusCode.OK, verification.StatusCode);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.False(string.IsNullOrWhiteSpace(auth.GetProperty("refreshToken").GetString()));
        Assert.Equal(Roles.User, auth.GetProperty("userProfile").GetProperty("role").GetString());

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", auth.GetProperty("accessToken").GetString());
        var profile = await client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        var user = await profile.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(username, user.GetProperty("data").GetProperty("username").GetString());
    }
}
