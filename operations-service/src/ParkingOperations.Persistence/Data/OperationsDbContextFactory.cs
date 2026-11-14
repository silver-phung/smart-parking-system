using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ParkingOperations.Persistence.Data;

public sealed class OperationsDbContextFactory : IDesignTimeDbContextFactory<OperationsDbContext>
{
    public OperationsDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__OperationsConnection");
        if (string.IsNullOrWhiteSpace(connection))
            connection = "Host=localhost;Database=operations_service;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<OperationsDbContext>()
            .UseNpgsql(connection)
            .Options;
        return new OperationsDbContext(options);
    }
}
