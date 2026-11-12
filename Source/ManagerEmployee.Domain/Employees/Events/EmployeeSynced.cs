using ManagerEmployee.Domain.Employees.Enums;

namespace ManagerEmployee.Domain.Employees.Events;

public sealed record EmployeeSynced
{
    public required EmployeeProvider Provider { get; init; }

    public required string ProviderEmployeeId { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }
}
