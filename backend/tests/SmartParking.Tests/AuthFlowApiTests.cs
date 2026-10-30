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
    public async Task EmailOnlyRegistration_RejectsWrongOtp_ThenAllowsLoginByEmail()
    {
        using var baseFactory = new CustomWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.PostConfigure<SmartParking.Application.Common.Options.RecaptchaOptions>(opt => opt.Enabled = false);
            services.PostConfigure<SmartParking.Application.Common.Options.EmailOptions>(opt => opt.Provider = "Mock");
            services.Remove(services.Single(d => d.ServiceType == typeof(IOtpService)));
            services.AddScoped<IOtpService, TestFixedOtpService>();
        }));
        using var client = factory.CreateClient();
        const string email = "email.only@example.invalid";
        const string password = "Email-only-test-123!";
        var registration = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var registered = (await registration.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.False(string.IsNullOrWhiteSpace(registered.GetProperty("username").GetString()));
        Assert.Equal(Roles.User, registered.GetProperty("role").GetString());
        var beforeVerification = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = email, password });
        Assert.False(beforeVerification.IsSuccessStatusCode);
        var wrongOtp = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new { email, code = "999999" });
        Assert.Equal(HttpStatusCode.BadRequest, wrongOtp.StatusCode);
        var verification = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new { email, code = TestFixedOtpService.FixedOtp });
        Assert.Equal(HttpStatusCode.OK, verification.StatusCode);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var profile = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("userProfile");
        Assert.True(profile.GetProperty("isEmailVerified").GetBoolean());
        Assert.Equal("APPROVED", profile.GetProperty("approvalStatus").GetString());
    }

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

    [Fact]
    public async Task CustomerFullLifecycle_Register_VerifyOtp_Login_Profile_Vehicle_Capacity_Booking_Sessions()
    {
        using var baseFactory = new CustomWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.PostConfigure<SmartParking.Application.Common.Options.RecaptchaOptions>(opt => opt.Enabled = false);
                services.PostConfigure<SmartParking.Application.Common.Options.EmailOptions>(opt => opt.Provider = "Mock");

                var descriptor = services.Single(d => d.ServiceType == typeof(IOtpService));
                services.Remove(descriptor);
                services.AddScoped<IOtpService, TestFixedOtpService>();
            });
        });

        using var client = factory.CreateClient();

        // 1. Register new customer
        var registerRes = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            username = "phung_demo_customer",
            email = "phung.demo@customer.vn",
            password = "CustomerPassword@123!",
            fullName = "Phung Demo Customer",
            phoneNumber = "0912345678"
        });
        Assert.Equal(HttpStatusCode.Created, registerRes.StatusCode);

        // 2. Verify Email with OTP Code
        var verifyRes = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new
        {
            email = "phung.demo@customer.vn",
            code = TestFixedOtpService.FixedOtp
        });
        Assert.Equal(HttpStatusCode.OK, verifyRes.StatusCode);

        // 3. Login
        var loginRes = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = "phung_demo_customer",
            password = "CustomerPassword@123!"
        });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        var loginData = (await loginRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        var accessToken = loginData.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // 4. User Profile (GET and PUT)
        var profileRes = await client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.OK, profileRes.StatusCode);
        var profileData = (await profileRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal("phung_demo_customer", profileData.GetProperty("username").GetString());
        Assert.Equal("phung.demo@customer.vn", profileData.GetProperty("email").GetString());

        var updateProfileRes = await client.PutAsJsonAsync("/api/v1/users/me", new
        {
            fullName = "Phung Demo Customer (Updated)",
            phoneNumber = "0987654321"
        });
        Assert.Equal(HttpStatusCode.OK, updateProfileRes.StatusCode);

        // 5. Vehicle CRUD
        var createVehicleRes = await client.PostAsJsonAsync("/api/v1/vehicles", new
        {
            plateNumber = "51H-999.88",
            brand = "Toyota",
            model = "Camry",
            color = "Black"
        });
        Assert.Equal(HttpStatusCode.Created, createVehicleRes.StatusCode);
        var vehicleData = (await createVehicleRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        var vehicleId = vehicleData.GetProperty("id").GetInt64();
        Assert.Equal("51H-999.88", vehicleData.GetProperty("plateNumber").GetString());

        var getVehiclesRes = await client.GetAsync("/api/v1/vehicles");
        Assert.Equal(HttpStatusCode.OK, getVehiclesRes.StatusCode);

        // 6. Public Parking Capacity
        var capacityRes = await client.GetAsync("/api/v1/parking-lots/1/capacity");
        Assert.Equal(HttpStatusCode.OK, capacityRes.StatusCode);
        var capacityData = (await capacityRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.True(capacityData.GetProperty("totalCapacity").GetInt32() > 0);

        // 7. Booking
        var createBookingRes = await client.PostAsJsonAsync("/api/v1/bookings", new
        {
            lotId = 1,
            vehicleId = vehicleId,
            plateNumber = "51H-999.88",
            startTime = DateTime.UtcNow.AddHours(1),
            endTime = DateTime.UtcNow.AddHours(3),
            notes = "Sprint 3 Demo Booking"
        });
        Assert.Equal(HttpStatusCode.Created, createBookingRes.StatusCode);
        var bookingData = (await createBookingRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        var bookingId = bookingData.GetProperty("id").GetInt64();
        Assert.True(bookingId > 0);

        // 8. My Bookings
        var myBookingsRes = await client.GetAsync("/api/v1/bookings/my");
        Assert.Equal(HttpStatusCode.OK, myBookingsRes.StatusCode);

        // 9. My Sessions (History)
        var mySessionsRes = await client.GetAsync("/api/v1/parking-sessions/my-sessions");
        Assert.Equal(HttpStatusCode.OK, mySessionsRes.StatusCode);
    }
}

public class TestFixedOtpService : IOtpService
{
    public const string FixedOtp = "123456";
    private readonly IOtpService _inner;

    public TestFixedOtpService(Microsoft.Extensions.Options.IOptions<SmartParking.Application.Common.Options.SecurityOptions> securityOptions)
    {
        _inner = new SmartParking.Infrastructure.Services.OtpService(securityOptions);
    }

    public string GenerateOtpCode() => FixedOtp;
    public string ComputeOtpHash(long userId, string tokenType, string otpCode) => _inner.ComputeOtpHash(userId, tokenType, otpCode);
    public bool VerifyOtpHash(long userId, string tokenType, string otpCode, string storedHash) => _inner.VerifyOtpHash(userId, tokenType, otpCode, storedHash);
}
