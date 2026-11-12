using ManagerEmployee.Application.Employees.Models;
using ManagerEmployee.Application.Employees.Ports;

namespace ManagerEmployee.Application.Employees.Queries.GetEmployee;

public sealed class GetEmployeeQueryHandler(IEmployeeReadStore employeeReadStore)
{
    public Task<EmployeeDetails?> HandleAsync(
        GetEmployeeQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.ProviderEmployeeId);

        return employeeReadStore.FindAsync(
            query.Provider,
            query.ProviderEmployeeId.Trim(),
            cancellationToken);
    }
}
