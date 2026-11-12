using System.Text.Json.Serialization;

namespace ManagerEmployee.Infrastructure.Provider2.Integration.Models;

internal sealed class ProviderEmployee
{
    [JsonPropertyName("employee_number")]
    public string? EmployeeNumber { get; init; }

    [JsonPropertyName("personal_info")]
    public PersonalInformation? PersonalInformation { get; init; }

    [JsonPropertyName("work_info")]
    public WorkInformation? WorkInformation { get; init; }
}

internal sealed class PersonalInformation
{
    [JsonPropertyName("given_name")]
    public string? GivenName { get; init; }

    [JsonPropertyName("family_name")]
    public string? FamilyName { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("mobile")]
    public string? Mobile { get; init; }
}

internal sealed class WorkInformation
{
    [JsonPropertyName("role")]
    public string? Role { get; init; }

    [JsonPropertyName("division")]
    public string? Division { get; init; }

    [JsonPropertyName("start_date")]
    public string? StartDate { get; init; }

    [JsonPropertyName("current_status")]
    public string? CurrentStatus { get; init; }
}
