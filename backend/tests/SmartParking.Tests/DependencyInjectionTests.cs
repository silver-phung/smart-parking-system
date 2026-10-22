using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.Application;
using SmartParking.Infrastructure;
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
}
