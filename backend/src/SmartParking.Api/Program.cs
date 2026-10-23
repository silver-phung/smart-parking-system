using Microsoft.OpenApi.Models;
using SmartParking.Application;
using SmartParking.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger / OpenAPI
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Smart Parking API",
        Version = "v1",
        Description = "API Foundation for Smart Parking System - FPT Software OJT"
    });
});

// Configure ASP.NET Core Health Checks
builder.Services.AddHealthChecks();

// Register Clean Architecture layers
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseDefaultFiles();
app.UseStaticFiles();

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

app.UseAuthorization();

// Map Health Check endpoint at /health (No auth required for local health check)
app.MapHealthChecks("/health");

app.MapControllers();
app.MapGet("/", () => Results.Redirect("/app/"));
app.MapFallbackToFile("/app/{*path:nonfile}", "app/index.html");

app.Run();

// Partial Program class declaration for WebApplicationFactory in integration tests
public partial class Program { }
