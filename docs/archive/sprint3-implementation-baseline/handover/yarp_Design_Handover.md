# Task: Implement YARP API Gateway with Load Balancing for All Microservice APIs

## 1. Objective

Implement a centralized API Gateway using ASP.NET Core Web API and YARP (Yet Another Reverse Proxy) for the existing Smart Parking microservices project.

The gateway must route **all existing API endpoints** from the frontend to their corresponding microservices, not just health-check endpoints or a limited set of example routes.

### Requirements

- Inspect the entire existing solution before making changes.
- Identify all microservices, their controllers/endpoints, HTTP methods, route prefixes, ports, and target .NET versions.
- Route all existing public APIs through the API Gateway.
- Support load balancing across multiple instances of the same microservice.
- Forward JWT access tokens and preserve the intended authentication and authorization behavior.
- Configure health checks for downstream service instances.
- Support environment-specific service addresses.
- Avoid modifying existing business logic unless necessary for gateway integration.

## 2. Inspect the Existing Project First

Before implementing anything:

1. Inspect the solution structure and existing `.sln` or `.slnx` files.
2. Identify all existing microservices, such as:
   - UserService
   - ParkingService
   - BookingService
   - ImageService
3. Discover the actual services in the repository instead of assuming that every example service exists.
4. Inspect controllers, minimal API mappings, route attributes, HTTP methods, authentication requirements, and existing middleware.
5. Identify existing health endpoints and service ports.
6. Record all API routes in an endpoint inventory.
7. Identify APIs that are internal-only and must not be exposed to the frontend.

Do not create duplicate services, controllers, or endpoints that already exist.

## 3. API Gateway Setup

Create or update an ASP.NET Core Web API project named `ApiGateway`.

Install YARP if it is not already installed:

```bash
dotnet add package Yarp.ReverseProxy
```

Use a package version compatible with the existing .NET target framework.

Register YARP in `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(
        builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Register required middleware here according to the
// project's authentication, logging, and security requirements.

app.MapReverseProxy();

app.Run();
```

Integrate this configuration into the existing application startup if the gateway project already exists. Do not overwrite unrelated middleware or configuration.

## 4. Route All Existing APIs

Configure YARP routes for every existing API that should be accessible through the gateway.

This includes, where present in the project:

- Authentication and account APIs
- User profile and user management APIs
- Parking lot and parking spot APIs
- Booking and reservation APIs
- Vehicle management APIs
- Payment APIs
- Image upload and image management APIs
- Administrative APIs
- Other existing microservice APIs discovered during inspection

The list above is illustrative. The actual route configuration must be based on the repository's existing APIs.

### Routing requirements

- Cover all applicable HTTP methods: GET, POST, PUT, PATCH, DELETE, and any other methods currently supported.
- Preserve query strings, request bodies, content types, and required headers.
- Support multipart/form-data requests for file uploads.
- Preserve downstream route paths unless an explicit path transformation is required.
- Configure specific routes before broad catch-all routes when necessary.
- Avoid overlapping routes that send requests to the wrong service.
- Do not expose internal-only service endpoints unintentionally.
- Do not invent API endpoints that do not exist in the project.

Prefer route patterns that cover an entire service API when its endpoint prefix is unambiguous.

For example, if all BookingService APIs use `/api/bookings`, configure a route that covers the full prefix and all nested paths rather than configuring only `GET /api/bookings`.

If different controllers use different route prefixes, account for all of them.

## 5. Configure Clusters and Load Balancing

Create a YARP cluster for each logical downstream microservice.

Each cluster must contain all configured instances of that same service.

Example:

```json
{
  "ReverseProxy": {
    "Routes": {
      "bookings-route": {
        "ClusterId": "bookings-cluster",
        "Match": {
          "Path": "/api/bookings/{**catch-all}"
        }
      }
    },
    "Clusters": {
      "bookings-cluster": {
        "LoadBalancingPolicy": "RoundRobin",
        "Destinations": {
          "booking-instance-1": {
            "Address": "http://localhost:5301/"
          },
          "booking-instance-2": {
            "Address": "http://localhost:5302/"
          }
        }
      }
    }
  }
}
```

This is an example only. Discover and configure the real route prefixes and service instances from the existing project.

### Load-balancing requirements

- Use `RoundRobin` as the initial demonstration policy unless the project requires another supported policy.
- Support multiple destinations for the same logical service.
- Do not load-balance requests between unrelated microservices.
- Verify that configured destinations are real, reachable instances.
- Do not assume that adding a second destination automatically starts another instance.
- Avoid introducing sticky sessions unless the application's requirements explicitly need them.

## 6. Health Checks

Health checks are used to monitor downstream service instances. They are not a replacement for routing the application's business APIs.

Requirements:

1. Reuse existing health endpoints where available.
2. Add health endpoints only when required and when they do not already exist.
3. Configure YARP active health checks for applicable clusters using the options supported by the installed YARP version.
4. Ensure health-check paths and response codes are correct for each service.
5. Verify that unhealthy destinations are handled according to the configured health policy.
6. Define suitable behavior when all destinations for a service are unavailable.

Do not restrict gateway routing to health endpoints. The gateway must route all intended application APIs independently of health-check configuration.

## 7. JWT Authentication and Authorization

- Forward the `Authorization` header containing the JWT access token.
- Preserve the existing authentication and authorization behavior.
- Determine whether JWT validation currently occurs in each service, at the gateway, or both.
- Configure gateway authentication only when appropriate for the existing architecture.
- Ensure protected APIs remain protected after being routed through the gateway.
- Never use refresh tokens as normal API access tokens.
- Never log access tokens or other sensitive authentication data.
- Do not bypass existing `[Authorize]`, role checks, or permission policies.
- Do not trust client-supplied user IDs when identity should be derived from a validated token.

Do not change token issuance, refresh-token rotation, or existing authorization rules unless explicitly required.

## 8. Environment-Specific Configuration

Keep downstream addresses configurable for development, testing, and production.

Requirements:

- Use `appsettings.json` for safe default configuration.
- Use `appsettings.Development.json` for local development overrides where appropriate.
- Support environment variables or deployment-specific configuration for production addresses.
- Do not hardcode production hostnames into application logic.
- Do not commit secrets or credentials.
- When using Docker, use the appropriate container DNS names rather than `localhost` to reach other containers.
- If the deployment platform provides service discovery or a load-balancing endpoint, integrate with the actual deployment architecture rather than inventing replica addresses.

## 9. Frontend Integration

The frontend must call the API Gateway instead of calling individual microservice addresses directly.

Example:

```javascript
const response = await fetch(
  `${API_GATEWAY_URL}/api/bookings`,
  {
    headers: {
      Authorization: `Bearer ${accessToken}`
    }
  }
);
```

`API_GATEWAY_URL` must come from the frontend's environment configuration.

Update existing frontend API clients or service modules to use the gateway base URL where needed. Preserve existing endpoint paths and request contracts.

Do not unnecessarily rewrite the frontend or business logic.

## 10. Testing Requirements

Test the actual APIs discovered in the repository, not only health endpoints.

### Routing tests

- Verify that every intended frontend-facing API is reachable through the gateway.
- Test each route using its existing HTTP methods.
- Test query parameters, JSON request bodies, and response status codes.
- Test multipart/form-data endpoints if the project supports image or file uploads.
- Verify that requests reach the correct microservice.
- Verify that unknown paths do not accidentally reach an unrelated service.

### Authentication tests

- Test public endpoints without a token.
- Test protected endpoints with a valid access token.
- Test protected endpoints with missing, invalid, and expired access tokens.
- Verify role and permission checks continue to work.

### Load-balancing tests

- Run at least two instances of one existing microservice when the environment supports it.
- Send repeated requests through the gateway.
- Verify that both instances receive requests according to the selected policy.
- Stop or make one instance unavailable and verify the configured failure and health-check behavior.

Do not create permanent public diagnostic endpoints solely for testing unless they are explicitly needed and secured appropriately.

## 11. Acceptance Criteria

The task is complete only when:

1. The existing solution has been inspected and the intended API inventory has been documented.
2. All intended frontend-facing APIs are routed through YARP.
3. All relevant HTTP methods and request types are supported.
4. Requests reach the correct downstream services.
5. Multiple instances of a service can receive traffic through load balancing.
6. Health checks monitor downstream destinations without replacing application routes.
7. JWT authentication and authorization remain correct.
8. Service addresses can be configured per environment.
9. Frontend API clients use the gateway base URL.
10. Existing business logic and service database ownership remain unchanged.
11. Routing, authentication, upload, and load-balancing tests pass where applicable.
12. Any unsupported routes, missing service instances, or deployment limitations are documented honestly.

## 12. Implementation Rules

- Inspect before editing.
- Reuse existing services and configurations.
- Do not implement only `/health` or a sample endpoint.
- Do not assume the illustrative service names, ports, or route prefixes are correct.
- Do not replace real project configuration with example configuration without reconciling existing settings.
- Do not add unrelated dependencies or redesign the microservice architecture.
- Make the smallest complete set of changes required to route all intended APIs.
- At the end, report the files changed, the routes configured, the load-balancing setup, the tests performed, and any remaining limitations.
