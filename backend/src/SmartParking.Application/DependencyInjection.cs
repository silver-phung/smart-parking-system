using Microsoft.Extensions.DependencyInjection;

namespace SmartParking.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Application layer services (validators, mappings, use cases) will be registered here.
        return services;
    }
}
