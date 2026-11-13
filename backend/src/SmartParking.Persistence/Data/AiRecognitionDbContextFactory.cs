using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SmartParking.Persistence.Data;

public sealed class AiRecognitionDbContextFactory : IDesignTimeDbContextFactory<AiRecognitionDbContext>
{
    public AiRecognitionDbContext CreateDbContext(string[] args)
    {
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "../SmartParking.Api");
        if (!Directory.Exists(basePath))
            basePath = Directory.GetCurrentDirectory();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("AiRecognitionConnection")
            ?? configuration.GetConnectionString("AiService")
            ?? "Host=localhost;Database=recognition_dev;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<AiRecognitionDbContext>()
            .UseNpgsql(connectionString, b => b.MigrationsAssembly(typeof(AiRecognitionDbContext).Assembly)
                .MigrationsHistoryTable("__AiRecognitionMigrationsHistory"))
            .Options;
        return new AiRecognitionDbContext(options);
    }
}
