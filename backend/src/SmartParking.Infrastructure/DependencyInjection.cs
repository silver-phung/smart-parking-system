using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SmartParking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Infrastructure services (e.g., PostgreSQL DbContext on Neon, background jobs, external services)
        // will be registered here as approved by Owner.
        return services;
    }
}
