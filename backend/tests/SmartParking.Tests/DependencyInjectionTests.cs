using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.Application;
using SmartParking.Application.Common.Interfaces;
using SmartParking.Infrastructure;
using SmartParking.Infrastructure.Data;
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
    public void Sprint4_UsesDedicatedCoreDatabaseAndRemoteRecognition()
    {
        var configuration = Sprint4Configuration("smartparking_core_sprint4");
        using var provider = new ServiceCollection().AddInfrastructureServices(configuration).BuildServiceProvider();
        using var scope = provider.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<SmartParkingDbContext>();
        Assert.Equal("smartparking_core_sprint4", db.Database.GetDbConnection().Database);
        Assert.Null(scope.ServiceProvider.GetService<AiRecognitionDbContext>());
        Assert.IsType<RecognitionServiceClient>(scope.ServiceProvider.GetRequiredService<IStaffDetectionService>());
        Assert.IsType<RecognitionServiceClient>(scope.ServiceProvider.GetRequiredService<IRecognitionEvidenceReader>());
    }

    [Theory]
    [InlineData("smartparking")]
    [InlineData("smartparking_identity")]
    [InlineData("smartparking_recognition")]
    public void Sprint4_RejectsOtherDatabasesForCore(string database)
    {
        Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddInfrastructureServices(Sprint4Configuration(database)));
    }

    private static IConfiguration Sprint4Configuration(string database) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Sprint4:Enabled"] = "true",
            ["Sprint4:RecognitionUrl"] = "http://127.0.0.1:5296/",
            ["Sprint4:RecognitionKey"] = new string('x', 32),
            ["ConnectionStrings:Sprint4Core"] = $"Host=localhost;Database={database};Username=test",
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=smartparking;Username=test",
            ["ConnectionStrings:AiRecognitionConnection"] = "Host=localhost;Database=smartparking_recognition;Username=test"
        }).Build();
}
