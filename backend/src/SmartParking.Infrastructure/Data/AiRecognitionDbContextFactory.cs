using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SmartParking.Infrastructure.Data;

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
            .AddUserSecrets(typeof(AiRecognitionDbContextFactory).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("AiRecognitionConnection")
            ?? "Host=localhost;Database=ai_recognition_design;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<AiRecognitionDbContext>()
            .UseNpgsql(connectionString, b => b.MigrationsAssembly(typeof(AiRecognitionDbContext).Assembly)
                .MigrationsHistoryTable("__AiRecognitionMigrationsHistory"))
            .Options;
        return new AiRecognitionDbContext(options);
    }
}
