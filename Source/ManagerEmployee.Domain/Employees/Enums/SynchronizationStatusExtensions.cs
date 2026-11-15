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

    /// <summary>
    /// Parses the exact, lowercase contract value (<c>pending</c>, <c>synced</c>, or <c>failed</c>).
    /// </summary>
    public static bool TryParseContractValue(string? value, out SynchronizationStatus status)
    {
        switch (value)
        {
            case "pending":
                status = SynchronizationStatus.Pending;
                return true;
            case "synced":
                status = SynchronizationStatus.Synced;
                return true;
            case "failed":
                status = SynchronizationStatus.Failed;
                return true;
            default:
                status = default;
                return false;
        }
    }
}
