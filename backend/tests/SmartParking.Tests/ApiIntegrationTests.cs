using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Common.Options;
using SmartParking.Domain.Constants;
using SmartParking.Domain.Entities;
using SmartParking.Infrastructure.Data;
using Xunit;

namespace SmartParking.Tests;

public class ApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private HttpClient CreateClientForRole(string role)
    {
        var client = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var db = scope.ServiceProvider.GetRequiredService<SmartParkingDbContext>();

        var roleId = role switch
        {
            Roles.Admin => 1,
            Roles.Manager => 2,
            Roles.Staff => 3,
            _ => 4
        };

        var userId = 9000L + roleId;
        var user = db.Users.Find(userId);
        if (user == null)
        {
            user = new User
            {
                Id = userId,
                Username = $"test_{role.ToLower()}",
                Email = $"{role.ToLower()}@smartparking.test",
                FullName = $"Test {role}",
                PasswordHash = "dummy_hash",
                RoleId = roleId,
                IsActive = true,
                ApprovalStatus = "APPROVED"
            };
            db.Users.Add(user);
            db.SaveChanges();
        }
        else
        {
            user.RoleId = roleId;
            user.IsActive = true;
            db.SaveChanges();
        }

        var token = tokenService.GenerateAccessToken(user, role);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
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
        Assert.Contains("/api/v1/vehicles", content);
        Assert.Contains("/api/v1/parking-sessions", content);
        Assert.Contains("/api/v1/parking-dispatches", content);
        Assert.Contains("/api/v1/staff-assignments", content);
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

    #region Role-Based Access Control (RBAC) 403 Forbidden Verifications

    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Staff)]
    [InlineData(Roles.User)]
    public async Task PatchParkingLotStatus_NonAdmin_Returns403Forbidden(string role)
    {
        var client = CreateClientForRole(role);
        var body = new StringContent("{\"isOpenForAdmission\":false}", Encoding.UTF8, "application/json");

        var response = await client.PatchAsync("/api/v1/parking-lots/1/status", body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Staff)]
    [InlineData(Roles.User)]
    public async Task PostParkingFloor_NonAdmin_Returns403Forbidden(string role)
    {
        var client = CreateClientForRole(role);
        var body = new StringContent("{\"floorNumber\":1,\"name\":\"Tầng 1\"}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/v1/parking-floors/lot/1", body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Staff)]
    [InlineData(Roles.User)]
    public async Task PutParkingFloor_NonAdmin_Returns403Forbidden(string role)
    {
        var client = CreateClientForRole(role);
        var body = new StringContent("{\"floorNumber\":1,\"name\":\"Tầng 1 mới\"}", Encoding.UTF8, "application/json");

        var response = await client.PutAsync("/api/v1/parking-floors/1", body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Staff)]
    [InlineData(Roles.User)]
    public async Task PostParkingSlot_NonAdmin_Returns403Forbidden(string role)
    {
        var client = CreateClientForRole(role);
        var body = new StringContent("{\"slotCode\":\"A-01\",\"slotType\":\"CAR_4_SEATS\"}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/v1/parking-slots/floor/1", body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Staff)]
    [InlineData(Roles.User)]
    public async Task PutParkingSlot_NonAdmin_Returns403Forbidden(string role)
    {
        var client = CreateClientForRole(role);
        var body = new StringContent("{\"slotCode\":\"A-01-RENAMED\",\"slotType\":\"CAR_4_SEATS\"}", Encoding.UTF8, "application/json");

        var response = await client.PutAsync("/api/v1/parking-slots/1", body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Staff)]
    [InlineData(Roles.User)]
    public async Task PatchParkingSlotStatus_NonAdmin_Returns403Forbidden(string role)
    {
        var client = CreateClientForRole(role);
        var body = new StringContent("{\"physicalStatus\":\"MAINTENANCE\"}", Encoding.UTF8, "application/json");

        var response = await client.PatchAsync("/api/v1/parking-slots/1/status", body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Staff)]
    [InlineData(Roles.User)]
    public async Task PostParkingLot_NonAdmin_Returns403Forbidden(string role)
    {
        var client = CreateClientForRole(role);
        var body = new StringContent("{\"code\":\"LOT-TEST\",\"name\":\"Bãi Test\",\"totalCapacity\":100}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/v1/parking-lots", body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Staff)]
    [InlineData(Roles.User)]
    public async Task PutParkingLot_NonAdmin_Returns403Forbidden(string role)
    {
        var client = CreateClientForRole(role);
        var body = new StringContent("{\"name\":\"Bãi Test Sửa\",\"totalCapacity\":150}", Encoding.UTF8, "application/json");

        var response = await client.PutAsync("/api/v1/parking-lots/1", body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Staff)]
    [InlineData(Roles.User)]
    public async Task DeleteParkingLot_NonAdmin_Returns403Forbidden(string role)
    {
        var client = CreateClientForRole(role);

        var response = await client.DeleteAsync("/api/v1/parking-lots/1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Staff)]
    [InlineData(Roles.User)]
    public async Task DeleteParkingFloor_NonAdmin_Returns403Forbidden(string role)
    {
        var client = CreateClientForRole(role);

        var response = await client.DeleteAsync("/api/v1/parking-floors/1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Staff)]
    [InlineData(Roles.User)]
    public async Task DeleteParkingSlot_NonAdmin_Returns403Forbidden(string role)
    {
        var client = CreateClientForRole(role);

        var response = await client.DeleteAsync("/api/v1/parking-slots/1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetVehicles_WithoutAuth_Returns401Unauthorized()
    {
        var response = await _client.GetAsync("/api/v1/vehicles");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostParkingSessionCheckIn_WithoutAuth_Returns401Unauthorized()
    {
        var body = new StringContent("{\"lotId\":1,\"plateNumber\":\"51A-12345\"}", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/v1/parking-sessions/check-in", body);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostParkingSessionCheckOut_WithoutAuth_Returns401Unauthorized()
    {
        var body = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/v1/parking-sessions/1/check-out", body);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Staff)]
    [InlineData(Roles.User)]
    public async Task PostParkingSessionCheckIn_NonAdmin_Returns403Forbidden(string role)
    {
        var client = CreateClientForRole(role);
        var body = new StringContent("{\"lotId\":1,\"plateNumber\":\"51A-12345\"}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/parking-sessions/check-in", body);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Staff)]
    [InlineData(Roles.User)]
    public async Task PostParkingSessionCheckOut_NonAdmin_Returns403Forbidden(string role)
    {
        var client = CreateClientForRole(role);
        var body = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/parking-sessions/1/check-out", body);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.User)]
    public async Task GetActiveParkingSessions_User_Returns403Forbidden(string role)
    {
        var client = CreateClientForRole(role);
        var response = await client.GetAsync("/api/v1/parking-sessions/active");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Staff)]
    public async Task GetActiveParkingSessions_ManagerAndStaff_Returns200Ok(string role)
    {
        var client = CreateClientForRole(role);
        var response = await client.GetAsync("/api/v1/parking-sessions/active");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Staff)]
    public async Task GetVehicles_ManagerOrStaff_Returns403Forbidden(string role)
    {
        var client = CreateClientForRole(role);
        var response = await client.GetAsync("/api/v1/vehicles");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostParkingDispatch_WithoutAuth_Returns401Unauthorized()
    {
        var body = new StringContent("{\"sessionId\":1}", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/v1/parking-dispatches", body);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Staff)]
    [InlineData(Roles.User)]
    public async Task PostParkingDispatch_NonAdmin_Returns403Forbidden(string role)
    {
        var client = CreateClientForRole(role);
        var body = new StringContent("{\"sessionId\":1}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/parking-dispatches", body);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Staff)]
    [InlineData(Roles.User)]
    public async Task PostParkingDispatchAssign_NonAdmin_Returns403Forbidden(string role)
    {
        var client = CreateClientForRole(role);
        var body = new StringContent("{\"slotId\":1}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/parking-dispatches/1/assign", body);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Staff)]
    [InlineData(Roles.User)]
    public async Task PostParkingDispatchConfirmOccupied_NonAdmin_Returns403Forbidden(string role)
    {
        var client = CreateClientForRole(role);
        var body = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/parking-dispatches/1/confirm-occupied", body);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Staff)]
    [InlineData(Roles.User)]
    public async Task PostParkingDispatchReportWrongSlot_NonAdmin_Returns403Forbidden(string role)
    {
        var client = CreateClientForRole(role);
        var body = new StringContent("{\"actualSlotId\":2}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/parking-dispatches/1/report-wrong-slot", body);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Staff)]
    [InlineData(Roles.User)]
    public async Task PostParkingDispatchCancel_NonAdmin_Returns403Forbidden(string role)
    {
        var client = CreateClientForRole(role);
        var body = new StringContent("{\"reason\":\"test\"}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/parking-dispatches/1/cancel", body);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetParkingDispatches_WithoutAuth_Returns401Unauthorized()
    {
        var response = await _client.GetAsync("/api/v1/parking-dispatches");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Staff)]
    [InlineData(Roles.User)]
    public async Task GetParkingDispatches_AuthenticatedRoles_Returns200Ok(string role)
    {
        var client = CreateClientForRole(role);
        var response = await client.GetAsync("/api/v1/parking-dispatches");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetStaffAssignments_WithoutAuth_Returns401Unauthorized()
    {
        var response = await _client.GetAsync("/api/v1/staff-assignments");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Staff)]
    [InlineData(Roles.User)]
    public async Task GetStaffAssignments_NonAdmin_Returns403Forbidden(string role)
    {
        var client = CreateClientForRole(role);
        var response = await client.GetAsync("/api/v1/staff-assignments");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region Stale JWT Token Dynamic Revocation Integration Tests

    [Fact]
    public async Task AdminApi_StaleToken_DemotedToUserInDatabase_Returns403Forbidden()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartParkingDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var user = new User
        {
            Id = 8001,
            Username = "demoted_admin_test",
            Email = "demoted_admin@test.com",
            FullName = "Demoted Admin",
            PasswordHash = "dummy",
            RoleId = 1, // Admin initially
            IsActive = true,
            ApprovalStatus = "APPROVED"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var token = tokenService.GenerateAccessToken(user, Roles.Admin);

        // Admin is demoted to USER in database
        user.RoleId = 4;
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/staff-assignments");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminApi_StaleToken_DisabledInDatabase_Returns401Unauthorized()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartParkingDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var user = new User
        {
            Id = 8002,
            Username = "disabled_admin_test",
            Email = "disabled_admin@test.com",
            FullName = "Disabled Admin",
            PasswordHash = "dummy",
            RoleId = 1, // Admin
            IsActive = true,
            ApprovalStatus = "APPROVED"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var token = tokenService.GenerateAccessToken(user, Roles.Admin);

        // User is disabled in database
        user.IsActive = false;
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/staff-assignments");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task VehiclesApi_StaleToken_DisabledUserInDatabase_Returns401Unauthorized()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartParkingDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var user = new User
        {
            Id = 8003,
            Username = "disabled_user_test",
            Email = "disabled_user@test.com",
            FullName = "Disabled User",
            PasswordHash = "dummy",
            RoleId = 4, // User
            IsActive = true,
            ApprovalStatus = "APPROVED"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var token = tokenService.GenerateAccessToken(user, Roles.User);

        // User is disabled in database
        user.IsActive = false;
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/vehicles");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ParkingManagementApi_StaleToken_DemotedAdminToUser_Returns403Forbidden()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartParkingDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var user = new User
        {
            Id = 8004,
            Username = "demoted_admin_pm_test",
            Email = "demoted_pm@test.com",
            FullName = "Demoted PM Admin",
            PasswordHash = "dummy",
            RoleId = 1,
            IsActive = true,
            ApprovalStatus = "APPROVED"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var token = tokenService.GenerateAccessToken(user, Roles.Admin);

        // Demote in DB
        user.RoleId = 4;
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var body = new StringContent("{\"name\":\"Test Lot\",\"address\":\"123\",\"totalCapacity\":100}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/v1/parking-lots", body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Auth_ValidSignatureToken_NonExistentUserIdInDb_Returns401Unauthorized()
    {
        using var scope = _factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        // Generate token for non-existent user Id = 888888
        var ghostUser = new User
        {
            Id = 888888,
            Username = "ghost_user",
            Email = "ghost@smartparking.test",
            FullName = "Ghost User",
            PasswordHash = "dummy"
        };
        var token = tokenService.GenerateAccessToken(ghostUser, Roles.Admin);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/staff-assignments");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Auth_MissingOrInvalidUserIdClaimInToken_Returns401Unauthorized()
    {
        using var scope = _factory.Services.CreateScope();
        var jwtOptions = scope.ServiceProvider.GetRequiredService<IOptions<JwtOptions>>().Value;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Token with valid signature and role, but with non-numeric UserId claim
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "invalid_non_numeric_id"),
            new(JwtRegisteredClaimNames.Sub, "invalid_non_numeric_id"),
            new(ClaimTypes.Role, Roles.Admin)
        };

        var jwtToken = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: jwtOptions.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds
        );
        var token = new JwtSecurityTokenHandler().WriteToken(jwtToken);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/staff-assignments");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion
}
