using ManagerEmployee.Domain.Employees.Enums;
using ManagerEmployee.Domain.Employees.Policies;

namespace ManagerEmployee.Domain.Employees;

public sealed record EmployeeProfile
{
    private EmployeeProfile(
        string firstName,
        string lastName,
        string email,
        string? phoneNumber,
        string? position,
        string? department,
        DateOnly? startDate,
        EmployeeStatus employmentStatus)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        PhoneNumber = phoneNumber;
        Position = position;
        Department = department;
        StartDate = startDate;
        EmploymentStatus = employmentStatus;
    }

    public string FirstName { get; }

    public string LastName { get; }

    public string Email { get; }

    public string? PhoneNumber { get; }

    public string? Position { get; }

    public string? Department { get; }

    public DateOnly? StartDate { get; }

    public EmployeeStatus EmploymentStatus { get; }

    public static EmployeeProfile Create(
        string firstName,
        string lastName,
        string email,
        string? phoneNumber,
        string? position,
        string? department,
        DateOnly? startDate,
        EmployeeStatus employmentStatus)
    {
        if (!Enum.IsDefined(employmentStatus))
        {
            throw new ArgumentOutOfRangeException(
                nameof(employmentStatus),
                employmentStatus,
                "Employment status is not supported.");
        }

        return new EmployeeProfile(
            Required(firstName, nameof(firstName), EmployeeFieldLimits.Name),
            Required(lastName, nameof(lastName), EmployeeFieldLimits.Name),
            ValidEmail(email),
            Optional(phoneNumber, nameof(phoneNumber), EmployeeFieldLimits.PhoneNumber),
            Optional(position, nameof(position), EmployeeFieldLimits.Position),
            Optional(department, nameof(department), EmployeeFieldLimits.Department),
            startDate,
            employmentStatus);
    }

    private static string ValidEmail(string value)
    {
        var normalizedEmail = Required(value, nameof(value), EmployeeFieldLimits.Email);
        if (!EmployeeEmailPolicy.IsValid(normalizedEmail))
        {
            throw new ArgumentException("Email must have a valid format.", nameof(value));
        }

        return normalizedEmail;
    }

    private static string Required(string value, string parameterName, int maximumLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalizedValue = value.Trim();
        if (normalizedValue.Length > maximumLength)
        {
            throw new ArgumentException(
                $"Value cannot exceed {maximumLength} characters.",
                parameterName);
        }

        return normalizedValue;
    }

    private static string? Optional(string? value, string parameterName, int maximumLength) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : Required(value, parameterName, maximumLength);
}
