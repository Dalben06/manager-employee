using System.Globalization;
using System.Net.Mail;
using ManagerEmployee.Application.Providers;
using ManagerEmployee.Domain.Employees.Policies;
using ManagerEmployee.Infrastructure.Provider1.Integration.Models;

namespace ManagerEmployee.Infrastructure.Provider1.Integration.Providers;

internal static class ProviderEmployeeValidator
{
    public static IReadOnlyList<ProviderValidationError> Validate(ProviderEmployee employee)
    {
        ArgumentNullException.ThrowIfNull(employee);

        var errors = new List<ProviderValidationError>();
        ValidateRequired(employee.EmployeeId, "emp_id", EmployeeFieldLimits.ProviderEmployeeId, errors);
        ValidateRequired(employee.FirstName, "first_name", EmployeeFieldLimits.Name, errors);
        ValidateRequired(employee.LastName, "last_name", EmployeeFieldLimits.Name, errors);
        ValidateEmail(employee.EmailAddress, "email_address", errors);
        ValidateOptional(employee.Phone, "phone", EmployeeFieldLimits.PhoneNumber, errors);
        ValidateOptional(employee.JobTitle, "job_title", EmployeeFieldLimits.Position, errors);
        ValidateOptional(employee.Department, "dept", EmployeeFieldLimits.Department, errors);
        ValidateDate(employee.HireDate, "hire_date", errors);
        ValidateStatus(employee.EmploymentStatus, errors);
        return errors;
    }

    private static void ValidateRequired(
        string? value,
        string field,
        int maximumLength,
        ICollection<ProviderValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new ProviderValidationError(field, $"{field} is required."));
            return;
        }

        ValidateMaximumLength(value, field, maximumLength, errors);
    }

    private static void ValidateOptional(
        string? value,
        string field,
        int maximumLength,
        ICollection<ProviderValidationError> errors)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            ValidateMaximumLength(value.Trim(), field, maximumLength, errors);
        }
    }

    private static void ValidateMaximumLength(
        string value,
        string field,
        int maximumLength,
        ICollection<ProviderValidationError> errors)
    {
        if (value.Trim().Length > maximumLength)
        {
            errors.Add(new ProviderValidationError(
                field,
                $"{field} cannot exceed {maximumLength} characters."));
        }
    }

    private static void ValidateEmail(
        string? value,
        string field,
        ICollection<ProviderValidationError> errors)
    {
        ValidateRequired(value, field, EmployeeFieldLimits.Email, errors);
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var normalized = value.Trim();
        if (!MailAddress.TryCreate(normalized, out var parsed) ||
            !string.Equals(parsed.Address, normalized, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new ProviderValidationError(field, $"{field} must have a valid email format."));
        }
    }

    private static void ValidateDate(
        string? value,
        string field,
        ICollection<ProviderValidationError> errors)
    {
        if (!string.IsNullOrWhiteSpace(value) &&
            !DateOnly.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
        {
            errors.Add(new ProviderValidationError(field, $"{field} must use the format YYYY-MM-DD."));
        }
    }

    private static void ValidateStatus(
        string? value,
        ICollection<ProviderValidationError> errors)
    {
        if (value is not ("active" or "inactive" or "terminated"))
        {
            errors.Add(new ProviderValidationError(
                "employment_status",
                "employment_status must be active, inactive, or terminated."));
        }
    }
}
