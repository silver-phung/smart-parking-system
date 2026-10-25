using Microsoft.Extensions.DependencyInjection;
using SmartParking.Application.Events;
using SmartParking.Application.Services;

namespace SmartParking.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ParkingLotService>();
        services.AddScoped<IoTService>();

        // Sprint 3 – Thức: Event Ingestion Pipeline
        services.AddScoped<ParkingEventService>();

        return services;
    }
}
