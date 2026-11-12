namespace ManagerEmployee.Application.Employees.Commands.SyncEmployee;

public enum SyncEmployeeOutcome
{
    Synchronized = 1,
    ValidationFailed = 2,
    UnknownProvider = 3,
    SynchronizationFailed = 4,
    PersistenceConflict = 5,
}
