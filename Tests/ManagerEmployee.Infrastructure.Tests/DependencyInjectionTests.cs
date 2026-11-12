using ManagerEmployee.Application.Employees.Ports;
using ManagerEmployee.Application.Events;
using ManagerEmployee.Domain.Employees.Enums;
using ManagerEmployee.Domain.Employees.Events;
using ManagerEmployee.Domain.Employees.Repositories;
using ManagerEmployee.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ManagerEmployee.Infrastructure.Tests;

[TestClass]
public sealed class DependencyInjectionTests
{
    [TestMethod]
    public void AddInfrastructure_RegistersDbContextAndEmployeeRepository()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(
            "Server=localhost;Database=RegistrationInspection;Integrated Security=True;TrustServerCertificate=True");

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        Assert.IsNotNull(scope.ServiceProvider.GetService<ManagerEmployeeDbContext>());
        Assert.IsNotNull(scope.ServiceProvider.GetService<IEmployeeRepository>());
        Assert.IsNotNull(scope.ServiceProvider.GetService<IEmployeeReadStore>());
        Assert.IsNotNull(scope.ServiceProvider.GetService<IEmployeeSyncedEventPublisher>());
    }

    [TestMethod]
    public async Task InMemoryPublisher_DispatchesEventToRegisteredHandlers()
    {
        var services = new ServiceCollection();
        var handler = new RecordingEmployeeSyncedEventHandler();
        services.AddSingleton<IEmployeeSyncedEventHandler>(handler);
        services.AddInfrastructure(
            "Server=localhost;Database=RegistrationInspection;Integrated Security=True;TrustServerCertificate=True");

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
