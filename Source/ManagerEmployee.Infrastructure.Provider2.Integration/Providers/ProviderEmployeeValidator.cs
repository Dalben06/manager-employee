using System.Globalization;
using System.Net.Mail;
using ManagerEmployee.Application.Providers;
using ManagerEmployee.Domain.Employees.Policies;
using ManagerEmployee.Infrastructure.Provider2.Integration.Models;

namespace ManagerEmployee.Infrastructure.Provider2.Integration.Providers;

internal static class ProviderEmployeeValidator
{
    public static IReadOnlyList<ProviderValidationError> Validate(ProviderEmployee employee)
    {
        ArgumentNullException.ThrowIfNull(employee);

        var errors = new List<ProviderValidationError>();
        ValidateRequired(
            employee.EmployeeNumber,
            "employee_number",
            EmployeeFieldLimits.ProviderEmployeeId,
            errors);

        if (employee.PersonalInformation is null)
        {
            errors.Add(new ProviderValidationError("personal_info", "personal_info is required."));
        }
        else
        {
            ValidateRequired(
                employee.PersonalInformation.GivenName,
                "personal_info.given_name",
                EmployeeFieldLimits.Name,
                errors);
            ValidateRequired(
                employee.PersonalInformation.FamilyName,
                "personal_info.family_name",
                EmployeeFieldLimits.Name,
                errors);
            ValidateEmail(employee.PersonalInformation.Email, "personal_info.email", errors);
            ValidateOptional(
                employee.PersonalInformation.Mobile,
                "personal_info.mobile",
                EmployeeFieldLimits.PhoneNumber,
                errors);
        }

        if (employee.WorkInformation is null)
        {
            errors.Add(new ProviderValidationError("work_info", "work_info is required."));
        }
        else
        {
            ValidateOptional(
                employee.WorkInformation.Role,
                "work_info.role",
                EmployeeFieldLimits.Position,
                errors);
            ValidateOptional(
                employee.WorkInformation.Division,
                "work_info.division",
                EmployeeFieldLimits.Department,
                errors);
            ValidateDate(employee.WorkInformation.StartDate, "work_info.start_date", errors);
            ValidateStatus(employee.WorkInformation.CurrentStatus, errors);
        }

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
            ValidateMaximumLength(value, field, maximumLength, errors);
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
        if (value is not ("employed" or "on_leave" or "terminated"))
        {
            errors.Add(new ProviderValidationError(
                "work_info.current_status",
                "work_info.current_status must be employed, on_leave, or terminated."));
        }
    }
}
