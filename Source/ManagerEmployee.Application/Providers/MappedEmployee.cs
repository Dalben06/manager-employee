using ManagerEmployee.Domain.Employees.Enums;

namespace ManagerEmployee.Application.Providers;

public sealed record MappedEmployee
{
    public required EmployeeProvider Provider { get; init; }

    public required string ProviderEmployeeId { get; init; }

    public required string FirstName { get; init; }

    public required string LastName { get; init; }

    public required string Email { get; init; }

    public string? PhoneNumber { get; init; }

    public string? Position { get; init; }

    public string? Department { get; init; }

    public DateOnly? StartDate { get; init; }

    public required EmployeeStatus EmploymentStatus { get; init; }
}
