using ManagerEmployee.Application.Employees.Models;
using ManagerEmployee.Domain.Employees.Enums;

namespace ManagerEmployee.Application.Employees.Ports;

public interface IEmployeeReadStore
{
    Task<EmployeeDetails?> FindAsync(
        EmployeeProvider provider,
        string providerEmployeeId,
        CancellationToken cancellationToken);
}
