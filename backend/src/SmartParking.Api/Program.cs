using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SmartParking.Application;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Application.Common.Options;
using SmartParking.Domain.Constants;
using SmartParking.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// 1. Configure Options & Security Secret Validation
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var securityOptions = builder.Configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>() ?? new SecurityOptions();
var recaptchaOptions = builder.Configuration.GetSection(RecaptchaOptions.SectionName).Get<RecaptchaOptions>() ?? new RecaptchaOptions();

// In Production, FAIL STARTUP immediately if secrets are missing or weak
if (builder.Environment.IsProduction())
{
    if (string.IsNullOrWhiteSpace(jwtOptions.SecretKey) || Encoding.UTF8.GetByteCount(jwtOptions.SecretKey) < 32)
    {
        throw new InvalidOperationException("FATAL: Jwt:SecretKey must be configured in Production with at least 32 bytes (256 bits).");
    }

    if (string.IsNullOrWhiteSpace(securityOptions.OtpSecretKey) || Encoding.UTF8.GetByteCount(securityOptions.OtpSecretKey) < 32)
    {
        throw new InvalidOperationException("FATAL: Security:OtpSecretKey must be configured in Production with at least 32 bytes (256 bits).");
    }

    if (!recaptchaOptions.Enabled || string.IsNullOrWhiteSpace(recaptchaOptions.SecretKey))
    {
        throw new InvalidOperationException("FATAL: reCAPTCHA must be enabled with a valid SecretKey in Production.");
    }
}
else
{
    // In Development/Testing: if secrets are not set via User Secrets or environment,
    // generate transient in-memory cryptographic keys for this run (NEVER hardcode secrets in source files)
    if (string.IsNullOrWhiteSpace(jwtOptions.SecretKey) || Encoding.UTF8.GetByteCount(jwtOptions.SecretKey) < 32)
    {
        var randomBytes = new byte[32];
        RandomNumberGenerator.Fill(randomBytes);
        jwtOptions.SecretKey = Convert.ToBase64String(randomBytes);
        builder.Configuration["Jwt:SecretKey"] = jwtOptions.SecretKey;
    }

    if (string.IsNullOrWhiteSpace(securityOptions.OtpSecretKey) || Encoding.UTF8.GetByteCount(securityOptions.OtpSecretKey) < 32)
    {
        var randomBytes = new byte[32];
        RandomNumberGenerator.Fill(randomBytes);
        securityOptions.OtpSecretKey = Convert.ToBase64String(randomBytes);
        builder.Configuration["Security:OtpSecretKey"] = securityOptions.OtpSecretKey;
    }
}

// 2. Configure JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = builder.Environment.IsProduction();
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = string.IsNullOrWhiteSpace(jwtOptions.Issuer) ? "SmartParkingApi" : jwtOptions.Issuer,
        ValidateAudience = true,
        ValidAudience = string.IsNullOrWhiteSpace(jwtOptions.Audience) ? "SmartParkingClient" : jwtOptions.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        RoleClaimType = ClaimTypes.Role,
        NameClaimType = ClaimTypes.Name
    };
});

// 3. Configure Role-based Authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Roles.Admin, policy => policy.RequireRole(Roles.Admin));
    options.AddPolicy(Roles.Operator, policy => policy.RequireRole(Roles.Operator, Roles.Admin));
    options.AddPolicy(Roles.Customer, policy => policy.RequireRole(Roles.Customer));
});

// 4. Configure Partitioned Rate Limiting (Per-IP partition against brute force and DDoS)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"message\":\"Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau ít phút.\"}",
            cancellationToken);
    };

    // Register policy: 5 requests / 5 minutes per IP
    options.AddPolicy("register-rate-limit", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0
            }));

    // Login policy: 10 requests / 1 minute per IP
    options.AddPolicy("login-rate-limit", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Verify Email policy: 10 requests / 1 minute per IP
    options.AddPolicy("verify-rate-limit", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Resend Verification policy: 3 requests / 5 minutes per IP
    options.AddPolicy("resend-rate-limit", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0
            }));
});

// 5. Configure Swagger / OpenAPI with JWT Bearer
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Smart Parking API",
        Version = "v1",
        Description = "API Foundation and Accounts Subsystem for Smart Parking System - FPT Software OJT"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập JWT Bearer token theo định dạng: Bearer {token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Configure ASP.NET Core Health Checks
builder.Services.AddHealthChecks();

// Register Clean Architecture layers
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Smart Parking API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Map Health Check endpoint at /health (No auth required for local health check)
app.MapHealthChecks("/health");

app.MapControllers();

// Execute safe idempotent Admin Bootstrap on startup
using (var scope = app.Services.CreateScope())
{
    try
    {
        var adminBootstrap = scope.ServiceProvider.GetRequiredService<IAdminBootstrapService>();
        await adminBootstrap.BootstrapAdminAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Admin bootstrap did not execute during application startup.");
    }
}

app.Run();

// Partial Program class declaration for WebApplicationFactory in integration tests
public partial class Program { }
