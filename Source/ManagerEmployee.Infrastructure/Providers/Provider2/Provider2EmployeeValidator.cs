using FluentValidation;
using ManagerEmployee.Domain.Employees.Policies;

namespace ManagerEmployee.Infrastructure.Providers.Provider2;

/// <summary>
/// Validates a Provider 2 payload. The provider schema requires only the nested objects, so the nested
/// fields TrackTik requires (names, email, status) are enforced here before anything is persisted.
/// </summary>
internal sealed class Provider2EmployeeValidator : AbstractValidator<Provider2Employee>
{
    public Provider2EmployeeValidator()
    {
        RuleFor(employee => employee.EmployeeNumber)
            .RequiredText("employee_number", EmployeeFieldLimits.ProviderEmployeeId);

        RuleFor(employee => employee.PersonalInformation)
            .NotNull()
            .WithMessage("personal_info is required.")
            .OverridePropertyName("personal_info");
        When(employee => employee.PersonalInformation is not null, () =>
        {
            RuleFor(employee => employee.PersonalInformation!.GivenName)
                .RequiredText("personal_info.given_name", EmployeeFieldLimits.Name);
            RuleFor(employee => employee.PersonalInformation!.FamilyName)
                .RequiredText("personal_info.family_name", EmployeeFieldLimits.Name);
            RuleFor(employee => employee.PersonalInformation!.Email)
                .RequiredEmail("personal_info.email");
            RuleFor(employee => employee.PersonalInformation!.Mobile)
                .OptionalText("personal_info.mobile", EmployeeFieldLimits.PhoneNumber);
        });

        RuleFor(employee => employee.WorkInformation)
            .NotNull()
            .WithMessage("work_info is required.")
            .OverridePropertyName("work_info");
        When(employee => employee.WorkInformation is not null, () =>
        {
            RuleFor(employee => employee.WorkInformation!.Role)
                .OptionalText("work_info.role", EmployeeFieldLimits.Position);
            RuleFor(employee => employee.WorkInformation!.Division)
                .OptionalText("work_info.division", EmployeeFieldLimits.Department);
            RuleFor(employee => employee.WorkInformation!.StartDate)
                .OptionalDate("work_info.start_date");
            RuleFor(employee => employee.WorkInformation!.CurrentStatus)
                .OneOf("work_info.current_status", "employed", "on_leave", "terminated");
        });
    }
}
