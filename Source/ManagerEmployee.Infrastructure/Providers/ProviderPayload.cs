using System.Text.Json;
using FluentValidation.Results;
using ManagerEmployee.Application.Providers;

namespace ManagerEmployee.Infrastructure.Providers;

/// <summary>
/// Deserialization and result helpers shared by provider mappers.
/// </summary>
internal static class ProviderPayload
{
    public static T? Deserialize<T>(JsonElement payload)
        where T : class
    {
        try
        {
            return payload.ValueKind == JsonValueKind.Object ? payload.Deserialize<T>() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static ProviderEmployeeMappingResult InvalidPayload(string providerName) =>
        ProviderEmployeeMappingResult.Failure([
            new ProviderValidationError("payload", $"Payload must be a valid {providerName} employee object."),
        ]);

    public static ProviderEmployeeMappingResult ValidationFailed(ValidationResult result) =>
        ProviderEmployeeMappingResult.Failure(result.Errors.Select(
            error => new ProviderValidationError(error.PropertyName, error.ErrorMessage)));

    public static string Required(string? value) => value!.Trim();

    public static string? Optional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static DateOnly? Date(string? value) =>
        !string.IsNullOrWhiteSpace(value) && ProviderFieldRules.TryParseDate(value, out var date)
            ? date
            : null;
}
