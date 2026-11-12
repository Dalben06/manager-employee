using ManagerEmployee.Application.Employees.Models;
using ManagerEmployee.Application.Employees.Ports;
using ManagerEmployee.Application.Synchronization;
using ManagerEmployee.Domain.Employees.Enums;
using Microsoft.EntityFrameworkCore;

namespace ManagerEmployee.Infrastructure.Persistence.ReadStores;

internal sealed class EmployeeReadStore(ManagerEmployeeDbContext dbContext) : IEmployeeReadStore
{
    public Task<EmployeeDetails?> FindAsync(
        EmployeeProvider provider,
        string providerEmployeeId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerEmployeeId);

        // The final projection runs client-side only for the derived TrackTik identifier.
        return dbContext.Employees
            .AsNoTracking()
            .Where(employee =>
                employee.Provider == provider &&
                employee.ProviderEmployeeId == providerEmployeeId)
            .Select(employee => new EmployeeDetails
            {
                Provider = employee.Provider,
                ProviderEmployeeId = employee.ProviderEmployeeId,
                TrackTikEmployeeId = TrackTikEmployeeIdentifier.Create(
                    employee.Provider,
                    employee.ProviderEmployeeId),
                FirstName = employee.FirstName,
                LastName = employee.LastName,
                Email = employee.Email,
                PhoneNumber = employee.PhoneNumber,
                Position = employee.Position,
                Department = employee.Department,
                StartDate = employee.StartDate,
                EmploymentStatus = employee.EmploymentStatus,
                SyncStatus = employee.SyncStatus,
                Attempts = employee.Attempts,
                LastAttemptAt = employee.LastAttemptAt,
                LastError = employee.LastError,
            })
            .SingleOrDefaultAsync(cancellationToken);
    }
}
