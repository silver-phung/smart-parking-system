using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SmartParking.Infrastructure.Data;

public class SmartParkingDbContextFactory : IDesignTimeDbContextFactory<SmartParkingDbContext>
{
    public SmartParkingDbContext CreateDbContext(string[] args)
    {
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "../SmartParking.Api");
        if (!Directory.Exists(basePath))
        {
            basePath = Directory.GetCurrentDirectory();
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Host=localhost;Database=smartparking_dev;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<SmartParkingDbContext>();
        optionsBuilder.UseNpgsql(connectionString, b => b.MigrationsAssembly(typeof(SmartParkingDbContext).Assembly.FullName));

        return new SmartParkingDbContext(optionsBuilder.Options);
    }
}
