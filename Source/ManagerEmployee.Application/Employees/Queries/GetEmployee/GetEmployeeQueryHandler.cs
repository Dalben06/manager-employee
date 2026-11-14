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

        // A blank identifier cannot match a stored employee; treat it as not found rather than an error.
        if (string.IsNullOrWhiteSpace(query.ProviderEmployeeId))
        {
            return Task.FromResult<EmployeeDetails?>(null);
        }

        return employeeReadStore.FindAsync(
            query.Provider,
            query.ProviderEmployeeId.Trim(),
            cancellationToken);
    }
}
