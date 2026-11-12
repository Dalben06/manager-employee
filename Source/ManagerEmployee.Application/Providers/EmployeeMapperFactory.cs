using ManagerEmployee.Domain.Employees.Enums;

namespace ManagerEmployee.Application.Providers;

public sealed class EmployeeMapperFactory : IEmployeeMapperFactory
{
    private readonly IReadOnlyDictionary<EmployeeProvider, IProviderEmployeeMapper> _mappers;

    public EmployeeMapperFactory(IEnumerable<IProviderEmployeeMapper> mappers)
    {
        ArgumentNullException.ThrowIfNull(mappers);
        _mappers = mappers.ToDictionary(mapper => mapper.Provider);
    }

    public bool TryGetMapper(string provider, out IProviderEmployeeMapper? mapper)
    {
        var parsedProvider = provider switch
        {
            "provider1" => EmployeeProvider.Provider1,
            "provider2" => EmployeeProvider.Provider2,
            _ => (EmployeeProvider?)null,
        };

        if (parsedProvider is null)
        {
            mapper = null;
            return false;
        }

        return _mappers.TryGetValue(parsedProvider.Value, out mapper);
    }
}
