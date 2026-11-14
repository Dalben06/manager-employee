using ManagerEmployee.Application.Employees.Models;
using ManagerEmployee.Domain.Employees.Enums;

namespace ManagerEmployee.Api.Contracts;

/// <summary>
/// The stored employee in TrackTik field names, plus its provider identity and synchronization state.
/// </summary>
public sealed record EmployeeResponse
{
    public required string Provider { get; init; }

    public required string ProviderEmployeeId { get; init; }

    /// <summary>The deterministic TrackTik identifier, <c>{provider}-{providerEmployeeId}</c>.</summary>
    public required string EmployeeId { get; init; }

    public required string FirstName { get; init; }

    public required string LastName { get; init; }

    public required string Email { get; init; }

    public string? PhoneNumber { get; init; }

    public string? Position { get; init; }

    public string? Department { get; init; }

    public DateOnly? StartDate { get; init; }

    /// <summary>TrackTik employment status: <c>active</c>, <c>inactive</c>, or <c>terminated</c>.</summary>
    public required string Status { get; init; }

    /// <summary>Synchronization status: <c>pending</c>, <c>synced</c>, or <c>failed</c>.</summary>
    public required string SyncStatus { get; init; }

    public required int Attempts { get; init; }

    public string? LastError { get; init; }

    public DateTimeOffset? LastAttemptAt { get; init; }

    public static EmployeeResponse From(EmployeeDetails employee)
    {
        ArgumentNullException.ThrowIfNull(employee);

        return new EmployeeResponse
        {
            Provider = employee.Provider.ToContractValue(),
            ProviderEmployeeId = employee.ProviderEmployeeId,
            EmployeeId = employee.TrackTikEmployeeId,
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            Email = employee.Email,
            PhoneNumber = employee.PhoneNumber,
            Position = employee.Position,
            Department = employee.Department,
            StartDate = employee.StartDate,
            Status = employee.EmploymentStatus.ToContractValue(),
            SyncStatus = employee.SyncStatus.ToContractValue(),
            Attempts = employee.Attempts,
            LastError = employee.LastError,
            LastAttemptAt = employee.LastAttemptAt,
        };
    }
}
