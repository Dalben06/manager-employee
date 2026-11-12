using ManagerEmployee.Domain.Employees;
using ManagerEmployee.Domain.Employees.Enums;
using ManagerEmployee.Domain.Employees.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ManagerEmployee.Infrastructure.Persistence.Repositories;

internal sealed class EmployeeRepository(ManagerEmployeeDbContext dbContext) : IEmployeeRepository
{
    public Task<Employee?> FindByIdentityAsync(
        EmployeeProvider provider,
        string providerEmployeeId,
        CancellationToken cancellationToken) =>
        dbContext.Employees.SingleOrDefaultAsync(
            employee => employee.Provider == provider && employee.ProviderEmployeeId == providerEmployeeId,
            cancellationToken);

    public async Task AddAsync(Employee employee, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(employee);

        await dbContext.Employees.AddAsync(employee, cancellationToken);
    }

    public async Task<EmployeeCommitOutcome> CommitAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return EmployeeCommitOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            return EmployeeCommitOutcome.Conflict;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return EmployeeCommitOutcome.Conflict;
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sqlException &&
        IsUniqueConstraintViolationNumber(sqlException.Number);

    internal static bool IsUniqueConstraintViolationNumber(int number) =>
        number is 2601 or 2627;
}
