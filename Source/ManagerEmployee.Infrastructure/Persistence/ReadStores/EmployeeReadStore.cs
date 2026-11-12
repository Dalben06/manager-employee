using ManagerEmployee.Application.Employees.Models;
using ManagerEmployee.Application.Employees.Ports;
using ManagerEmployee.Application.Synchronization;
using ManagerEmployee.Domain.Employees.Enums;
using Microsoft.EntityFrameworkCore;

namespace ManagerEmployee.Infrastructure.Persistence.ReadStores;

internal sealed class EmployeeReadStore(ManagerEmployeeDbContext dbContext) : IEmployeeReadStore
{
    public async Task<EmployeeDetails?> FindAsync(
        EmployeeProvider provider,
        string providerEmployeeId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerEmployeeId);

        var employee = await dbContext.Employees
            .AsNoTracking()
            .Where(candidate =>
                candidate.Provider == provider &&
                candidate.ProviderEmployeeId == providerEmployeeId)
            .Select(candidate => new EmployeeReadRow
            {
                Provider = candidate.Provider,
                ProviderEmployeeId = candidate.ProviderEmployeeId,
                FirstName = candidate.FirstName,
                LastName = candidate.LastName,
                Email = candidate.Email,
                PhoneNumber = candidate.PhoneNumber,
                Position = candidate.Position,
                Department = candidate.Department,
                StartDate = candidate.StartDate,
                EmploymentStatus = candidate.EmploymentStatus,
                SyncStatus = candidate.SyncStatus,
                Attempts = candidate.Attempts,
                LastAttemptAt = candidate.LastAttemptAt,
                LastError = candidate.LastError,
            })
            .SingleOrDefaultAsync(cancellationToken);

        return employee?.ToDetails();
    }

    private sealed class EmployeeReadRow
    {
        public EmployeeProvider Provider { get; init; }

        public required string ProviderEmployeeId { get; init; }

        public required string FirstName { get; init; }

        public required string LastName { get; init; }

        public required string Email { get; init; }

        public string? PhoneNumber { get; init; }

        public string? Position { get; init; }

        public string? Department { get; init; }

        public DateOnly? StartDate { get; init; }

        public EmployeeStatus EmploymentStatus { get; init; }

        public SynchronizationStatus SyncStatus { get; init; }

        public int Attempts { get; init; }

        public DateTimeOffset? LastAttemptAt { get; init; }

        public string? LastError { get; init; }

        public EmployeeDetails ToDetails() => new()
        {
            Provider = Provider,
            ProviderEmployeeId = ProviderEmployeeId,
            TrackTikEmployeeId = TrackTikEmployeeIdentifier.Create(Provider, ProviderEmployeeId),
            FirstName = FirstName,
            LastName = LastName,
            Email = Email,
            PhoneNumber = PhoneNumber,
            Position = Position,
            Department = Department,
            StartDate = StartDate,
            EmploymentStatus = EmploymentStatus,
            SyncStatus = SyncStatus,
            Attempts = Attempts,
            LastAttemptAt = LastAttemptAt,
            LastError = LastError,
        };
    }
}
