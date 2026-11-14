using ManagerEmployee.Application.Employees.Models;
using ManagerEmployee.Application.Providers;

namespace ManagerEmployee.Application.Employees.Commands.SyncEmployee;

/// <param name="Outcome">The business outcome of the submission.</param>
/// <param name="Employee">The stored employee after the attempt, when one was persisted.</param>
/// <param name="ValidationErrors">Provider validation errors when the payload was rejected.</param>
/// <param name="EmployeeCreated">True when this submission stored the employee for the first time.</param>
public sealed record SyncEmployeeResult(
    SyncEmployeeOutcome Outcome,
    EmployeeDetails? Employee,
    IReadOnlyList<ProviderValidationError> ValidationErrors,
    bool EmployeeCreated)
{
    public static SyncEmployeeResult Synchronized(EmployeeDetails employee, bool employeeCreated) =>
        new(SyncEmployeeOutcome.Synchronized, employee, [], employeeCreated);

    public static SyncEmployeeResult SynchronizationFailed(EmployeeDetails employee) =>
        new(SyncEmployeeOutcome.SynchronizationFailed, employee, [], false);

    public static SyncEmployeeResult ValidationFailed(IReadOnlyList<ProviderValidationError> errors) =>
        new(SyncEmployeeOutcome.ValidationFailed, null, errors, false);

    public static SyncEmployeeResult UnknownProvider() =>
        new(SyncEmployeeOutcome.UnknownProvider, null, [], false);

    public static SyncEmployeeResult PersistenceConflict() =>
        new(SyncEmployeeOutcome.PersistenceConflict, null, [], false);
}
