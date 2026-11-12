namespace ManagerEmployee.Domain.Employees.Policies;

public static class EmployeeFieldLimits
{
    public const int ProviderEmployeeId = 256;
    public const int Name = 200;
    public const int Email = 320;
    public const int PhoneNumber = 50;
    public const int Position = 200;
    public const int Department = 200;
    public const int LastError = 2000;
}
