namespace ManagerEmployee.Domain.Employees.Enums;

public static class EmployeeProviderExtensions
{
    public static string ToContractValue(this EmployeeProvider provider) => provider switch
    {
        EmployeeProvider.Provider1 => "provider1",
        EmployeeProvider.Provider2 => "provider2",
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Provider is not supported."),
    };

    /// <summary>
    /// Parses the exact, lowercase provider route value (<c>provider1</c> or <c>provider2</c>).
    /// </summary>
    public static bool TryParseContractValue(string? value, out EmployeeProvider provider)
    {
        switch (value)
        {
            case "provider1":
                provider = EmployeeProvider.Provider1;
                return true;
            case "provider2":
                provider = EmployeeProvider.Provider2;
                return true;
            default:
                provider = default;
                return false;
        }
    }
}
