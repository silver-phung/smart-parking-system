using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ParkingOperations.Application.CashHandovers;
using ParkingOperations.Infrastructure;
using ParkingOperations.Persistence.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];
var jwtSecret = builder.Configuration["Jwt:SecretKey"];
if (string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience) ||
    string.IsNullOrWhiteSpace(jwtSecret) || Encoding.UTF8.GetByteCount(jwtSecret) < 32)
    throw new InvalidOperationException("Operations Service requires Jwt:Issuer, Jwt:Audience, and a shared Jwt:SecretKey of at least 32 bytes.");

var operationsConnection = builder.Configuration.GetConnectionString("OperationsConnection");
if (string.IsNullOrWhiteSpace(operationsConnection))
    throw new InvalidOperationException("ConnectionStrings:OperationsConnection must point to the dedicated Operations database.");

var coreBaseUrl = builder.Configuration["CoreApi:BaseUrl"];
if (!Uri.TryCreate(coreBaseUrl, UriKind.Absolute, out var coreUri) ||
    (coreUri.Scheme != Uri.UriSchemeHttps && !(coreUri.Scheme == Uri.UriSchemeHttp && coreUri.IsLoopback)) ||
    !string.IsNullOrEmpty(coreUri.UserInfo) || coreUri.Query.Length > 0 || coreUri.Fragment.Length > 0)
    throw new InvalidOperationException("CoreApi:BaseUrl must be an HTTPS service origin (HTTP is allowed only on loopback).");

builder.Services.AddDbContext<OperationsDbContext>(options => options.UseNpgsql(operationsConnection));
builder.Services.AddScoped<ICashHandoverService, CashHandoverService>();
builder.Services.AddHttpClient<ICoreStaffAccessClient, CoreStaffAccessClient>(client =>
{
    client.BaseAddress = new Uri(coreUri.ToString().TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(10);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = builder.Environment.IsProduction();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.Name
        };
    });
builder.Services.AddAuthorization();
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (allowedOrigins.Length > 0)
        policy.WithOrigins(allowedOrigins).WithMethods("GET", "POST", "OPTIONS")
            .WithHeaders("Authorization", "Content-Type", "Accept");
}));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "parking-operations-service"
}));

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
