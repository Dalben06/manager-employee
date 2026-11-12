namespace ManagerEmployee.Application.Providers;

public interface IEmployeeMapperFactory
{
    bool TryGetMapper(string provider, out IProviderEmployeeMapper? mapper);
}
