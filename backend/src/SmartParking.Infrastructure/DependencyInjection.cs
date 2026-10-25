using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Infrastructure.Repositories;

namespace SmartParking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var relativePath = configuration["ParkingLots:DataFile"] ?? "Data/parking-lots.json";
        var fullPath = Path.Combine(AppContext.BaseDirectory, relativePath);
        if (!File.Exists(fullPath))
        {
            fullPath = Path.GetFullPath(relativePath);
        }
        services.AddScoped<IParkingLotRepository>(_ => new ParkingLotRepository(fullPath));

        // Sprint 3 – Thức: Event Ingestion Pipeline (in-memory, sẵn sàng chuyển sang EF khi DB hoàn tất)
        services.AddSingleton<IParkingEventLogRepository, InMemoryParkingEventLogRepository>();
        services.AddSingleton<IParkingSessionRepository,  InMemoryParkingSessionRepository>();

        return services;
    }
}
