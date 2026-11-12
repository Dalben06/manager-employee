using ManagerEmployee.Domain.Employees.Enums;
using ManagerEmployee.Domain.Employees.Policies;

namespace ManagerEmployee.Domain.Employees;

public sealed record EmployeeIdentity
{
    private EmployeeIdentity(EmployeeProvider provider, string providerEmployeeId)
    {
        Provider = provider;
        ProviderEmployeeId = providerEmployeeId;
    }

    public EmployeeProvider Provider { get; }

    public string ProviderEmployeeId { get; }

    public static EmployeeIdentity Create(
        EmployeeProvider provider,
        string providerEmployeeId)
    {
        if (!Enum.IsDefined(provider))
        {
            throw new ArgumentOutOfRangeException(
                nameof(provider),
                provider,
                "Provider is not supported.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(providerEmployeeId);
        var normalizedProviderEmployeeId = providerEmployeeId.Trim();
        if (normalizedProviderEmployeeId.Length > EmployeeFieldLimits.ProviderEmployeeId)
        {
            throw new ArgumentException(
                $"Provider employee ID cannot exceed {EmployeeFieldLimits.ProviderEmployeeId} characters.",
                nameof(providerEmployeeId));
        }

        return new EmployeeIdentity(provider, normalizedProviderEmployeeId);
    }
}
