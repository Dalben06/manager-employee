using ManagerEmployee.Domain.Employees.Policies;

namespace ManagerEmployee.Domain.Employees;

public sealed record SynchronizationError
{
    private SynchronizationError(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static SynchronizationError Create(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        var normalizedError = error.Trim();
        return new SynchronizationError(
            normalizedError.Length <= EmployeeFieldLimits.LastError
                ? normalizedError
                : normalizedError[..EmployeeFieldLimits.LastError]);
    }
}
