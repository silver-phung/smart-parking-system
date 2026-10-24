using Microsoft.Extensions.DependencyInjection;
using SmartParking.Application.Services;

namespace SmartParking.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ParkingLotService>();
        services.AddScoped<IoTService>();
        return services;
    }
}
