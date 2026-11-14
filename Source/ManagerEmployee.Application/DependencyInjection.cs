using ManagerEmployee.Application.Employees.Commands.SyncEmployee;
using ManagerEmployee.Application.Employees.Queries.GetEmployee;
using ManagerEmployee.Application.Synchronization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ManagerEmployee.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the employee use-case handlers and the process-local synchronization coordinator.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<SyncEmployeeCommandHandler>();
        services.AddScoped<GetEmployeeQueryHandler>();
        services.AddSingleton<EmployeeSynchronizationCoordinator>();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
