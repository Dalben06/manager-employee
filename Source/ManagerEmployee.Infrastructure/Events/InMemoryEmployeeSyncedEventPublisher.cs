using ManagerEmployee.Application.Events;
using ManagerEmployee.Domain.Employees.Events;

namespace ManagerEmployee.Infrastructure.Events;

internal sealed class InMemoryEmployeeSyncedEventPublisher(
    IEnumerable<IEmployeeSyncedEventHandler> handlers) : IEmployeeSyncedEventPublisher
{
    public async Task PublishAsync(
        EmployeeSynced notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        foreach (var handler in handlers)
        {
            await handler.HandleAsync(notification, cancellationToken);
        }
    }
}
