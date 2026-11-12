using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Persistence.Data;
using SmartParking.Persistence.Repositories;

namespace SmartParking.Persistence;

public static class PersistenceDependencyInjection
{
    public static IServiceCollection AddParkingPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<SmartParkingDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<SmartParkingDbContext>());
        services.AddScoped<IParkingOperationsWriter, ParkingOperationsWriter>();
        return services;
    }

    public static IServiceCollection AddRecognitionPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("AiRecognitionConnection");
        if (string.IsNullOrWhiteSpace(connection))
            connection = configuration.GetConnectionString("AiService");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Configure ConnectionStrings:AiRecognitionConnection for the Recognition Service.");

        services.AddDbContext<AiRecognitionDbContext>(options =>
            options.UseNpgsql(connection, postgres => postgres.MigrationsHistoryTable("__AiRecognitionMigrationsHistory")));
        return services;
    }
}
