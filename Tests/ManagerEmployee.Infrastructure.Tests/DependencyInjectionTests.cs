using ManagerEmployee.Application.Employees.Ports;
using ManagerEmployee.Application.Events;
using ManagerEmployee.Application.Synchronization;
using ManagerEmployee.Domain.Employees.Enums;
using ManagerEmployee.Domain.Employees.Events;
using ManagerEmployee.Domain.Employees.Repositories;
using ManagerEmployee.Infrastructure.Persistence;
using ManagerEmployee.Infrastructure.TrackTik;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ManagerEmployee.Infrastructure.Tests;

[TestClass]
public sealed class DependencyInjectionTests
{
    [TestMethod]
    public void AddInfrastructure_RegistersDbContextAndEmployeeRepository()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(TestConfiguration.Create());

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        Assert.IsNotNull(scope.ServiceProvider.GetService<ManagerEmployeeDbContext>());
        Assert.IsNotNull(scope.ServiceProvider.GetService<IEmployeeRepository>());
        Assert.IsNotNull(scope.ServiceProvider.GetService<IEmployeeReadStore>());
        Assert.IsNotNull(scope.ServiceProvider.GetService<IEmployeeSyncedEventPublisher>());
    }

    [TestMethod]
    public void AddInfrastructure_RegistersTrackTikClientFromConfiguration()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(TestConfiguration.Create());

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsNotNull(serviceProvider.GetService<ITrackTikEmployeeClient>());
        var options = serviceProvider.GetRequiredService<IOptions<TrackTikOptions>>().Value;
        Assert.AreEqual("https://tracktik.test/v1", options.ApiBaseUrl);
        Assert.AreEqual("https://tracktik.test/oauth/token", options.TokenUrl);
    }

    [TestMethod]
    [DataRow("TrackTik:ClientId", "TrackTik:ClientId and TrackTik:ClientSecret are required")]
    [DataRow("TrackTik:ClientSecret", "TrackTik:ClientId and TrackTik:ClientSecret are required")]
    [DataRow("TrackTik:TokenUrl", "TrackTik:ApiBaseUrl and TrackTik:TokenUrl must be absolute")]
    public void AddInfrastructure_InvalidTrackTikSettings_FailsValidationWithoutLeakingValues(
        string missingKey,
        string expectedMessage)
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(TestConfiguration.Create(new() { [missingKey] = string.Empty }));

        using var serviceProvider = services.BuildServiceProvider();

        var exception = Assert.ThrowsExactly<OptionsValidationException>(
            () => serviceProvider.GetRequiredService<IOptions<TrackTikOptions>>().Value);
        StringAssert.Contains(exception.Message, expectedMessage);
        Assert.DoesNotContain(TestConfiguration.ClientSecret, exception.Message);
    }

    [TestMethod]
    public void AddInfrastructure_MissingConnectionString_Throws()
    {
        var services = new ServiceCollection();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => services.AddInfrastructure(
            TestConfiguration.Create(new() { ["ConnectionStrings:ManagerEmployeeDatabase"] = null })));

        StringAssert.Contains(exception.Message, "ManagerEmployeeDatabase");
    }

    [TestMethod]
    public async Task InMemoryPublisher_DispatchesEventToRegisteredHandlers()
    {
        var services = new ServiceCollection();
        var handler = new RecordingEmployeeSyncedEventHandler();
        services.AddSingleton<IEmployeeSyncedEventHandler>(handler);
        services.AddInfrastructure(TestConfiguration.Create());

        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IEmployeeSyncedEventPublisher>();
        var notification = new EmployeeSynced
        {
            Provider = EmployeeProvider.Provider1,
            ProviderEmployeeId = "P1_001",
            OccurredAt = new DateTimeOffset(2026, 11, 11, 12, 30, 0, TimeSpan.Zero),
        };

        await publisher.PublishAsync(notification, CancellationToken.None);

        Assert.AreSame(notification, handler.Notification);
    }

    private sealed class RecordingEmployeeSyncedEventHandler : IEmployeeSyncedEventHandler
    {
        public EmployeeSynced? Notification { get; private set; }

        public Task HandleAsync(EmployeeSynced notification, CancellationToken cancellationToken)
        {
            Notification = notification;
            return Task.CompletedTask;
        }
    }
}
