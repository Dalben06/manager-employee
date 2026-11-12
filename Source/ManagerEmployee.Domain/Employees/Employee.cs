using ManagerEmployee.Domain.Employees.Enums;
using ManagerEmployee.Domain.Employees.Events;

namespace ManagerEmployee.Domain.Employees;

public sealed class Employee
{
    private Employee()
    {
    }

    public EmployeeProvider Provider { get; private set; }

    public string ProviderEmployeeId { get; private set; } = null!;

    public string FirstName { get; private set; } = null!;

    public string LastName { get; private set; } = null!;

    public string Email { get; private set; } = null!;

    public string? PhoneNumber { get; private set; }

    public string? Position { get; private set; }

    public string? Department { get; private set; }

    public DateOnly? StartDate { get; private set; }

    public EmployeeStatus EmploymentStatus { get; private set; }

    public SynchronizationStatus SyncStatus { get; private set; }

    public int Attempts { get; private set; }

    public DateTimeOffset? LastAttemptAt { get; private set; }

    public string? LastError { get; private set; }

    public bool HasBeenCreatedInTrackTik { get; private set; }

    public static Employee CreateForSynchronization(
        EmployeeIdentity identity,
        EmployeeSynchronizationAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(attempt);

        var employee = new Employee
        {
            Provider = identity.Provider,
            ProviderEmployeeId = identity.ProviderEmployeeId,
            SyncStatus = SynchronizationStatus.Pending,
            Attempts = 1,
            LastAttemptAt = attempt.AttemptedAt,
        };

        employee.ApplyProfile(attempt.Profile);
        return employee;
    }

    public void PrepareResubmission(EmployeeSynchronizationAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        var nextAttempts = checked(Attempts + 1);

        ApplyProfile(attempt.Profile);
        Attempts = nextAttempts;
        LastAttemptAt = attempt.AttemptedAt;
        SyncStatus = SynchronizationStatus.Pending;
    }

    public EmployeeSynced MarkSynced(DateTimeOffset occurredAt)
    {
        EnsureSynchronizationPending();
        if (occurredAt == default)
        {
            throw new ArgumentException("Event occurrence time is required.", nameof(occurredAt));
        }

        SyncStatus = SynchronizationStatus.Synced;
        LastError = null;
        HasBeenCreatedInTrackTik = true;

        return new EmployeeSynced
        {
            Provider = Provider,
            ProviderEmployeeId = ProviderEmployeeId,
            OccurredAt = occurredAt,
        };
    }

    public void MarkFailed(SynchronizationError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        EnsureSynchronizationPending();

        LastError = error.Value;
        SyncStatus = SynchronizationStatus.Failed;
    }

    private void ApplyProfile(EmployeeProfile profile)
    {
        FirstName = profile.FirstName;
        LastName = profile.LastName;
        Email = profile.Email;
        PhoneNumber = profile.PhoneNumber;
        Position = profile.Position;
        Department = profile.Department;
        StartDate = profile.StartDate;
        EmploymentStatus = profile.EmploymentStatus;
    }

    private void EnsureSynchronizationPending()
    {
        if (SyncStatus != SynchronizationStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending synchronization can be completed.");
        }
    }
}
