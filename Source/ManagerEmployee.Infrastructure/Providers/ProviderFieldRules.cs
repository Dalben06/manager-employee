using System.Globalization;
using FluentValidation;
using ManagerEmployee.Domain.Employees.Policies;

namespace ManagerEmployee.Infrastructure.Providers;

/// <summary>
/// Field rules shared by provider validators. Each rule reports errors under the provider's JSON field name.
/// </summary>
internal static class ProviderFieldRules
{
    public static IRuleBuilderOptions<T, string?> RequiredText<T>(
        this IRuleBuilder<T, string?> rule,
        string field,
        int maximumLength) =>
        rule
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage($"{field} is required.")
            .Must(value => value is null || value.Trim().Length <= maximumLength)
            .WithMessage($"{field} cannot exceed {maximumLength} characters.")
            .OverridePropertyName(field);

    public static IRuleBuilderOptions<T, string?> OptionalText<T>(
        this IRuleBuilder<T, string?> rule,
        string field,
        int maximumLength) =>
        rule
            .Must(value => string.IsNullOrWhiteSpace(value) || value.Trim().Length <= maximumLength)
            .WithMessage($"{field} cannot exceed {maximumLength} characters.")
            .OverridePropertyName(field);

    public static IRuleBuilderOptions<T, string?> RequiredEmail<T>(
        this IRuleBuilder<T, string?> rule,
        string field) =>
        rule
            .RequiredText(field, EmployeeFieldLimits.Email)
            .Must(value => string.IsNullOrWhiteSpace(value) || EmployeeEmailPolicy.IsValid(value))
            .WithMessage($"{field} must have a valid email format.");

    public static IRuleBuilderOptions<T, string?> OptionalDate<T>(
        this IRuleBuilder<T, string?> rule,
        string field) =>
        rule
            .Must(value => string.IsNullOrWhiteSpace(value) || TryParseDate(value, out _))
            .WithMessage($"{field} must use the format YYYY-MM-DD.")
            .OverridePropertyName(field);

    public static IRuleBuilderOptions<T, string?> OneOf<T>(
        this IRuleBuilder<T, string?> rule,
        string field,
        params string[] allowedValues) =>
        rule
            .Must(value => value is not null && allowedValues.Contains(value, StringComparer.Ordinal))
            .WithMessage($"{field} must be one of: {string.Join(", ", allowedValues)}.")
            .OverridePropertyName(field);

    public static bool TryParseDate(string value, out DateOnly date) =>
        DateOnly.TryParseExact(
            value.Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
}
