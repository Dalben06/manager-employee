using ManagerEmployee.Domain.Employees.Enums;

namespace ManagerEmployee.Application.Synchronization;

public static class TrackTikEmployeeIdentifier
{
    public static string Create(EmployeeProvider provider, string providerEmployeeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerEmployeeId);
        return $"{provider.ToContractValue()}-{providerEmployeeId.Trim()}";
    }
}
