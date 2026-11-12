using System.Text.Json.Serialization;

namespace ManagerEmployee.Infrastructure.Provider1.Integration.Models;

internal sealed class ProviderEmployee
{
    [JsonPropertyName("emp_id")]
    public string? EmployeeId { get; init; }

    [JsonPropertyName("first_name")]
    public string? FirstName { get; init; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; init; }

    [JsonPropertyName("email_address")]
    public string? EmailAddress { get; init; }

    [JsonPropertyName("phone")]
    public string? Phone { get; init; }

    [JsonPropertyName("job_title")]
    public string? JobTitle { get; init; }

    [JsonPropertyName("dept")]
    public string? Department { get; init; }

    [JsonPropertyName("hire_date")]
    public string? HireDate { get; init; }

    [JsonPropertyName("employment_status")]
    public string? EmploymentStatus { get; init; }
}
