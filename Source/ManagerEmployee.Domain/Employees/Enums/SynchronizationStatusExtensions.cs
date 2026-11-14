namespace ManagerEmployee.Domain.Employees.Enums;

public static class SynchronizationStatusExtensions
{
    /// <summary>
    /// Returns the contract value (<c>pending</c>, <c>synced</c>, or <c>failed</c>).
    /// </summary>
    public static string ToContractValue(this SynchronizationStatus status) => status switch
    {
        SynchronizationStatus.Pending => "pending",
        SynchronizationStatus.Synced => "synced",
        SynchronizationStatus.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Synchronization status is not supported."),
    };
}
