using System.Globalization;
using System.Text.Json;
using ManagerEmployee.Application.Providers;
using ManagerEmployee.Domain.Employees.Enums;
using ManagerEmployee.Infrastructure.Provider2.Integration.Models;

namespace ManagerEmployee.Infrastructure.Provider2.Integration.Providers;

public sealed class Provider2EmployeeMapper : IProviderEmployeeMapper
{
    public EmployeeProvider Provider => EmployeeProvider.Provider2;

    public ProviderEmployeeMappingResult Map(JsonElement payload)
    {
        ProviderEmployee? providerEmployee;

        try
        {
            providerEmployee = payload.Deserialize<ProviderEmployee>();
        }
        catch (JsonException)
        {
            return InvalidPayload();
        }

        if (providerEmployee is null)
        {
            return InvalidPayload();
        }

        var errors = ProviderEmployeeValidator.Validate(providerEmployee);
        if (errors.Count > 0)
        {
            return ProviderEmployeeMappingResult.Failure(errors);
        }

        var personalInformation = providerEmployee.PersonalInformation!;
        var workInformation = providerEmployee.WorkInformation!;
        return ProviderEmployeeMappingResult.Success(new MappedEmployee
        {
            Provider = Provider,
            ProviderEmployeeId = providerEmployee.EmployeeNumber!.Trim(),
            FirstName = personalInformation.GivenName!.Trim(),
            LastName = personalInformation.FamilyName!.Trim(),
            Email = personalInformation.Email!.Trim(),
            PhoneNumber = NormalizeOptional(personalInformation.Mobile),
            Position = NormalizeOptional(workInformation.Role),
            Department = NormalizeOptional(workInformation.Division),
            StartDate = ParseDate(workInformation.StartDate),
            EmploymentStatus = MapStatus(workInformation.CurrentStatus!),
        });
    }

    private static ProviderEmployeeMappingResult InvalidPayload() =>
        ProviderEmployeeMappingResult.Failure([
            new ProviderValidationError("payload", "Payload must be a valid Provider 2 employee object."),
        ]);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateOnly? ParseDate(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static EmployeeStatus MapStatus(string value) => value switch
    {
        "employed" => EmployeeStatus.Active,
        "on_leave" => EmployeeStatus.Inactive,
        "terminated" => EmployeeStatus.Terminated,
        _ => throw new InvalidOperationException("Provider 2 status was not validated."),
    };
}
