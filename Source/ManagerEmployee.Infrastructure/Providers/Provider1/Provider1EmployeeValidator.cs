using FluentValidation;
using ManagerEmployee.Domain.Employees.Policies;

namespace ManagerEmployee.Infrastructure.Providers.Provider1;

/// <summary>
/// Validates a Provider 1 payload against its schema and the fields TrackTik requires
/// (<c>employment_status</c> is optional in the provider schema but TrackTik requires a status).
/// </summary>
internal sealed class Provider1EmployeeValidator : AbstractValidator<Provider1Employee>
{
    public Provider1EmployeeValidator()
    {
        RuleFor(employee => employee.EmployeeId).RequiredText("emp_id", EmployeeFieldLimits.ProviderEmployeeId);
        RuleFor(employee => employee.FirstName).RequiredText("first_name", EmployeeFieldLimits.Name);
        RuleFor(employee => employee.LastName).RequiredText("last_name", EmployeeFieldLimits.Name);
        RuleFor(employee => employee.EmailAddress).RequiredEmail("email_address");
        RuleFor(employee => employee.Phone).OptionalText("phone", EmployeeFieldLimits.PhoneNumber);
        RuleFor(employee => employee.JobTitle).OptionalText("job_title", EmployeeFieldLimits.Position);
        RuleFor(employee => employee.Department).OptionalText("dept", EmployeeFieldLimits.Department);
        RuleFor(employee => employee.HireDate).OptionalDate("hire_date");
        RuleFor(employee => employee.EmploymentStatus)
            .OneOf("employment_status", "active", "inactive", "terminated");
    }
}
