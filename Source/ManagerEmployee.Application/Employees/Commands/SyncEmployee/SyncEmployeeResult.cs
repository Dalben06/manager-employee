using ManagerEmployee.Application.Employees.Models;
using ManagerEmployee.Application.Providers;

namespace ManagerEmployee.Application.Employees.Commands.SyncEmployee;

public sealed record SyncEmployeeResult(
    SyncEmployeeOutcome Outcome,
    EmployeeDetails? Employee,
    IReadOnlyList<ProviderValidationError> ValidationErrors)
{
    public static SyncEmployeeResult Synchronized(EmployeeDetails employee) =>
        new(SyncEmployeeOutcome.Synchronized, employee, []);

    public static SyncEmployeeResult SynchronizationFailed(EmployeeDetails employee) =>
        new(SyncEmployeeOutcome.SynchronizationFailed, employee, []);

    public static SyncEmployeeResult ValidationFailed(IReadOnlyList<ProviderValidationError> errors) =>
        new(SyncEmployeeOutcome.ValidationFailed, null, errors);

    public static SyncEmployeeResult UnknownProvider() =>
        new(SyncEmployeeOutcome.UnknownProvider, null, []);

    public static SyncEmployeeResult PersistenceConflict() =>
        new(SyncEmployeeOutcome.PersistenceConflict, null, []);
}
