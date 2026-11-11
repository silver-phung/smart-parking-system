using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.Application;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Infrastructure;
using SmartParking.Persistence.Data;
using SmartParking.Persistence;
using SmartParking.Infrastructure.Services;
using Xunit;

namespace SmartParking.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddApplicationServices_ShouldRegisterServicesWithoutExceptions()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.AddApplicationServices();

        // Assert
        Assert.NotNull(result);
        Assert.Same(services, result);
    }

    [Fact]
    public void AddInfrastructureServices_ShouldRegisterServicesWithoutExceptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var result = services.AddInfrastructureServices(configuration);

        // Assert
        Assert.NotNull(result);
        Assert.Same(services, result);
    }

    [Fact]
    public void Core_UsesExistingDatabaseAndRemoteRecognition()
    {
        var configuration = RecognitionConfiguration("recognition_test");
        using var provider = new ServiceCollection().AddParkingPersistence(configuration)
            .AddInfrastructureServices(configuration).BuildServiceProvider();
        using var scope = provider.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<SmartParkingDbContext>();
        Assert.Equal("smartparking", db.Database.GetDbConnection().Database);
        Assert.Null(scope.ServiceProvider.GetService<AiRecognitionDbContext>());
        Assert.IsType<RecognitionServiceClient>(scope.ServiceProvider.GetRequiredService<IStaffDetectionService>());
    }

    [Fact]
    public void Recognition_RequiresItsOwnConfiguredConnection()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddRecognitionPersistence(new ConfigurationBuilder().Build()));
    }

    [Fact]
    public void Recognition_UsesOnlyAiPersistence()
    {
        using var provider = new ServiceCollection()
            .AddRecognitionPersistence(RecognitionConfiguration("recognition_test")).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AiRecognitionDbContext>();
        Assert.Equal("recognition_test", db.Database.GetDbConnection().Database);
        Assert.Null(scope.ServiceProvider.GetService<SmartParkingDbContext>());
    }

    private static IConfiguration RecognitionConfiguration(string database) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Recognition:BaseUrl"] = "http://127.0.0.1:5296/",
            ["Recognition:InternalKey"] = new string('x', 32),
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=smartparking;Username=test",
            ["ConnectionStrings:AiRecognitionConnection"] = $"Host=localhost;Database={database};Username=test"
        }).Build();
}
