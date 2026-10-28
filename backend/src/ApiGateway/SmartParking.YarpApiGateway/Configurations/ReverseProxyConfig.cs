using Yarp.ReverseProxy.Configuration;

namespace SmartParking.YarpApiGateway.Configurations;

public static class ReverseProxyConfig
{
    public static IReadOnlyList<RouteConfig> GetRoutes()
    {
        return
        [
            new RouteConfig
            {
                RouteId = "api",
                ClusterId = "smartParkingApi",
                Match = new RouteMatch { Path = "/api/{**catch-all}" }
            },
            new RouteConfig
            {
                RouteId = "health",
                ClusterId = "smartParkingApi",
                Match = new RouteMatch { Path = "/health" }
            },
            new RouteConfig
            {
                RouteId = "swagger",
                ClusterId = "smartParkingApi",
                Match = new RouteMatch { Path = "/swagger/{**catch-all}" }
            }
        ];
    }

    public static IReadOnlyList<ClusterConfig> GetClusters(IConfiguration configuration)
    {
        var destinations = new Dictionary<string, DestinationConfig>();
        foreach (var destination in configuration
                     .GetSection("DownstreamServices:SmartParkingApi:Destinations")
                     .GetChildren())
        {
            if (string.IsNullOrWhiteSpace(destination.Value))
            {
                throw new InvalidOperationException(
                    $"Downstream destination '{destination.Key}' must have an address.");
            }

            destinations.Add(
                destination.Key,
                new DestinationConfig { Address = destination.Value });
        }

        if (destinations.Count == 0)
        {
            throw new InvalidOperationException(
                "Configure at least one destination under DownstreamServices:SmartParkingApi:Destinations.");
        }

        foreach (var destination in destinations)
        {
            if (!Uri.TryCreate(destination.Value.Address, UriKind.Absolute, out var address)
                || address.Scheme is not ("http" or "https"))
            {
                throw new InvalidOperationException(
                    $"Downstream destination '{destination.Key}' must be an absolute HTTP or HTTPS URL.");
            }
        }

        return
        [
            new ClusterConfig
            {
                ClusterId = "smartParkingApi",
                LoadBalancingPolicy = "RoundRobin",
                HealthCheck = new HealthCheckConfig
                {
                    Active = new ActiveHealthCheckConfig
                    {
                        Enabled = true,
                        Interval = TimeSpan.FromSeconds(10),
                        Timeout = TimeSpan.FromSeconds(5),
                        Policy = "ConsecutiveFailures",
                        Path = "/health"
                    }
                },
                Destinations = destinations
            }
        ];
    }
}
