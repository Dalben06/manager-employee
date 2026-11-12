using ManagerEmployee.Application.Synchronization;
using ManagerEmployee.Domain.Employees;
using ManagerEmployee.Domain.Employees.Enums;

namespace ManagerEmployee.Application.Employees.Models;

public sealed record EmployeeDetails
{
    public required EmployeeProvider Provider { get; init; }

    public required string ProviderEmployeeId { get; init; }

    public required string TrackTikEmployeeId { get; init; }

    public required string FirstName { get; init; }

    public required string LastName { get; init; }

    public required string Email { get; init; }

    public string? PhoneNumber { get; init; }

    public string? Position { get; init; }

    public string? Department { get; init; }

    public DateOnly? StartDate { get; init; }

    public required EmployeeStatus EmploymentStatus { get; init; }

    public required SynchronizationStatus SyncStatus { get; init; }

    public required int Attempts { get; init; }

    public DateTimeOffset? LastAttemptAt { get; init; }

    public string? LastError { get; init; }

    internal static EmployeeDetails FromDomain(Employee employee)
    {
        ArgumentNullException.ThrowIfNull(employee);

        return new EmployeeDetails
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
        };
    }
}
