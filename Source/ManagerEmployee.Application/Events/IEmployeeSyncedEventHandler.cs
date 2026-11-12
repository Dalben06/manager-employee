using ManagerEmployee.Domain.Employees.Events;

namespace ManagerEmployee.Application.Events;

public interface IEmployeeSyncedEventHandler
{
    Task HandleAsync(EmployeeSynced notification, CancellationToken cancellationToken);
}
