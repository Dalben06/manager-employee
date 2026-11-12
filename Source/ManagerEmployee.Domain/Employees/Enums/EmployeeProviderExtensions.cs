namespace ManagerEmployee.Domain.Employees.Enums;

public static class EmployeeProviderExtensions
{
    public static string ToContractValue(this EmployeeProvider provider) => provider switch
    {
        EmployeeProvider.Provider1 => "provider1",
        EmployeeProvider.Provider2 => "provider2",
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Provider is not supported."),
    };
}
