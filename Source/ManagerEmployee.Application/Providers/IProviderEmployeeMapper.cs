using System.Text.Json;
using ManagerEmployee.Domain.Employees.Enums;

namespace ManagerEmployee.Application.Providers;

public interface IProviderEmployeeMapper
{
    EmployeeProvider Provider { get; }

    ProviderEmployeeMappingResult Map(JsonElement payload);
}
