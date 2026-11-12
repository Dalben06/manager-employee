using ManagerEmployee.Domain.Employees.Events;

namespace ManagerEmployee.Application.Events;

public interface IEmployeeSyncedEventPublisher
{
    Task PublishAsync(EmployeeSynced notification, CancellationToken cancellationToken);
}
