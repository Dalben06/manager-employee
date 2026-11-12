namespace ManagerEmployee.Domain.Employees;

public sealed record EmployeeSynchronizationAttempt
{
    private EmployeeSynchronizationAttempt(EmployeeProfile profile, DateTimeOffset attemptedAt)
    {
        Profile = profile;
        AttemptedAt = attemptedAt;
    }

    public EmployeeProfile Profile { get; }

    public DateTimeOffset AttemptedAt { get; }

    public static EmployeeSynchronizationAttempt Create(
        EmployeeProfile profile,
        DateTimeOffset attemptedAt)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (attemptedAt == default)
        {
            throw new ArgumentException(
                "Synchronization attempt time is required.",
                nameof(attemptedAt));
        }

        return new EmployeeSynchronizationAttempt(profile, attemptedAt);
    }
}
