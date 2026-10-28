using SmartParking.YarpApiGateway.Configurations;

var builder = WebApplication.CreateBuilder(args);
builder.Services
    .AddReverseProxy()
    .LoadFromMemory(
        ReverseProxyConfig.GetRoutes(),
        ReverseProxyConfig.GetClusters(builder.Configuration));
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/gateway/health");
app.MapReverseProxy();

app.Run();

public partial class Program { }
