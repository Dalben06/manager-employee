using ManagerEmployee.Application.Employees.Ports;
using ManagerEmployee.Application.Events;
using ManagerEmployee.Domain.Employees.Repositories;
using ManagerEmployee.Infrastructure.Events;
using ManagerEmployee.Infrastructure.Persistence;
using ManagerEmployee.Infrastructure.Persistence.ReadStores;
using ManagerEmployee.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ManagerEmployee.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<ManagerEmployeeDbContext>(
            options => options.UseSqlServer(connectionString));
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IEmployeeReadStore, EmployeeReadStore>();
        services.AddScoped<IEmployeeSyncedEventPublisher, InMemoryEmployeeSyncedEventPublisher>();

        return services;
    }
}
