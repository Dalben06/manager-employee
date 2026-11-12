using ManagerEmployee.Domain.Employees.Enums;

namespace ManagerEmployee.Application.Employees.Queries.GetEmployee;

public sealed record GetEmployeeQuery(
    EmployeeProvider Provider,
    string ProviderEmployeeId);
