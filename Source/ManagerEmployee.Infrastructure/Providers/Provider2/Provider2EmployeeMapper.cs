using System.Text.Json;
using ManagerEmployee.Application.Providers;
using ManagerEmployee.Domain.Employees.Enums;

namespace ManagerEmployee.Infrastructure.Providers.Provider2;

public sealed class Provider2EmployeeMapper : IProviderEmployeeMapper
{
    private static readonly Provider2EmployeeValidator Validator = new();

    public EmployeeProvider Provider => EmployeeProvider.Provider2;

    public ProviderEmployeeMappingResult Map(JsonElement payload)
    {
        var employee = ProviderPayload.Deserialize<Provider2Employee>(payload);
        if (employee is null)
        {
            return ProviderPayload.InvalidPayload("Provider 2");
        }

        var validation = Validator.Validate(employee);
        if (!validation.IsValid)
        {
            return ProviderPayload.ValidationFailed(validation);
        }

        var personal = employee.PersonalInformation!;
        var work = employee.WorkInformation!;
        return ProviderEmployeeMappingResult.Success(new MappedEmployee
        {
            Provider = Provider,
            ProviderEmployeeId = ProviderPayload.Required(employee.EmployeeNumber),
            FirstName = ProviderPayload.Required(personal.GivenName),
            LastName = ProviderPayload.Required(personal.FamilyName),
            Email = ProviderPayload.Required(personal.Email),
            PhoneNumber = ProviderPayload.Optional(personal.Mobile),
            Position = ProviderPayload.Optional(work.Role),
            Department = ProviderPayload.Optional(work.Division),
            StartDate = ProviderPayload.Date(work.StartDate),
            EmploymentStatus = work.CurrentStatus switch
            {
                "employed" => EmployeeStatus.Active,
                "on_leave" => EmployeeStatus.Inactive,
                _ => EmployeeStatus.Terminated,
            },
        });
    }
}
