namespace ManagerEmployee.Domain.Employees.Enums;

public static class EmployeeStatusExtensions
{
    /// <summary>
    /// Returns the TrackTik contract value (<c>active</c>, <c>inactive</c>, or <c>terminated</c>).
    /// </summary>
    public static string ToContractValue(this EmployeeStatus status) => status switch
    {
        EmployeeStatus.Active => "active",
        EmployeeStatus.Inactive => "inactive",
        EmployeeStatus.Terminated => "terminated",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Employment status is not supported."),
    };
}
