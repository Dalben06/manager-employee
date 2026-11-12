using System.Text.Json;
using ManagerEmployee.Application.Providers;
using ManagerEmployee.Domain.Employees.Enums;

namespace ManagerEmployee.Infrastructure.Providers.Provider1;

public sealed class Provider1EmployeeMapper : IProviderEmployeeMapper
{
    private static readonly Provider1EmployeeValidator Validator = new();

    public EmployeeProvider Provider => EmployeeProvider.Provider1;

    public ProviderEmployeeMappingResult Map(JsonElement payload)
    {
        var employee = ProviderPayload.Deserialize<Provider1Employee>(payload);
        if (employee is null)
        {
            return ProviderPayload.InvalidPayload("Provider 1");
        }

        var validation = Validator.Validate(employee);
        if (!validation.IsValid)
        {
            return ProviderPayload.ValidationFailed(validation);
        }

        return ProviderEmployeeMappingResult.Success(new MappedEmployee
        {
            Provider = Provider,
            ProviderEmployeeId = ProviderPayload.Required(employee.EmployeeId),
            FirstName = ProviderPayload.Required(employee.FirstName),
            LastName = ProviderPayload.Required(employee.LastName),
            Email = ProviderPayload.Required(employee.EmailAddress),
            PhoneNumber = ProviderPayload.Optional(employee.Phone),
            Position = ProviderPayload.Optional(employee.JobTitle),
            Department = ProviderPayload.Optional(employee.Department),
            StartDate = ProviderPayload.Date(employee.HireDate),
            EmploymentStatus = employee.EmploymentStatus switch
            {
                "active" => EmployeeStatus.Active,
                "inactive" => EmployeeStatus.Inactive,
                _ => EmployeeStatus.Terminated,
            },
        });
    }
}
