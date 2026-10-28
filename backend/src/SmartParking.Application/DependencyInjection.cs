using Microsoft.Extensions.DependencyInjection;
using SmartParking.Application.Common.Interfaces;
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
        // Use factory so DI resolves IVehicleCompatibilityChecker (registered in Infrastructure)
        services.AddScoped<ParkingEventService>(sp =>
            new ParkingEventService(
                sp.GetRequiredService<IParkingEventLogRepository>(),
                sp.GetRequiredService<IParkingSessionRepository>(),
                sp.GetRequiredService<IVehicleCompatibilityChecker>()));

        return services;
    }
}
