using System.Globalization;
using System.Text.Json;
using ManagerEmployee.Application.Providers;
using ManagerEmployee.Domain.Employees.Enums;
using ManagerEmployee.Infrastructure.Provider1.Integration.Models;

namespace ManagerEmployee.Infrastructure.Provider1.Integration.Providers;

public sealed class Provider1EmployeeMapper : IProviderEmployeeMapper
{
    public EmployeeProvider Provider => EmployeeProvider.Provider1;

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

        return ProviderEmployeeMappingResult.Success(new MappedEmployee
        {
            Provider = Provider,
            ProviderEmployeeId = providerEmployee.EmployeeId!.Trim(),
            FirstName = providerEmployee.FirstName!.Trim(),
            LastName = providerEmployee.LastName!.Trim(),
            Email = providerEmployee.EmailAddress!.Trim(),
            PhoneNumber = NormalizeOptional(providerEmployee.Phone),
            Position = NormalizeOptional(providerEmployee.JobTitle),
            Department = NormalizeOptional(providerEmployee.Department),
            StartDate = ParseDate(providerEmployee.HireDate),
            EmploymentStatus = MapStatus(providerEmployee.EmploymentStatus!),
        });
    }

    private static ProviderEmployeeMappingResult InvalidPayload() =>
        ProviderEmployeeMappingResult.Failure([
            new ProviderValidationError("payload", "Payload must be a valid Provider 1 employee object."),
        ]);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateOnly? ParseDate(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static EmployeeStatus MapStatus(string value) => value switch
    {
        "active" => EmployeeStatus.Active,
        "inactive" => EmployeeStatus.Inactive,
        "terminated" => EmployeeStatus.Terminated,
        _ => throw new InvalidOperationException("Provider 1 status was not validated."),
    };
}
