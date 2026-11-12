using ManagerEmployee.Domain.Employees.Enums;

namespace ManagerEmployee.Domain.Employees.Repositories;

public interface IEmployeeRepository
{
    Task<Employee?> FindByIdentityAsync(
        EmployeeProvider provider,
        string providerEmployeeId,
        CancellationToken cancellationToken);

    Task AddAsync(Employee employee, CancellationToken cancellationToken);

    Task<EmployeeCommitOutcome> CommitAsync(CancellationToken cancellationToken);
}
