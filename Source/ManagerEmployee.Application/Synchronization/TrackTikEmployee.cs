using ManagerEmployee.Domain.Employees;
using ManagerEmployee.Domain.Employees.Enums;

namespace ManagerEmployee.Application.Synchronization;

public sealed record TrackTikEmployee
{
    public required string EmployeeId { get; init; }

    public required string FirstName { get; init; }

    public required string LastName { get; init; }

    public required string Email { get; init; }

    public string? PhoneNumber { get; init; }

    public string? Position { get; init; }

    public string? Department { get; init; }

    public DateOnly? StartDate { get; init; }

    public required EmployeeStatus Status { get; init; }

    public static TrackTikEmployee FromDomain(Employee employee)
    {
        ArgumentNullException.ThrowIfNull(employee);

        return new TrackTikEmployee
        {
            EmployeeId = TrackTikEmployeeIdentifier.Create(
                employee.Provider,
                employee.ProviderEmployeeId),
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            Email = employee.Email,
            PhoneNumber = employee.PhoneNumber,
            Position = employee.Position,
            Department = employee.Department,
            StartDate = employee.StartDate,
            Status = employee.EmploymentStatus,
        };
    }
}
