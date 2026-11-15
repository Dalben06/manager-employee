namespace ManagerEmployee.Application.Employees.Models;

/// <summary>
/// Employee counts per synchronization status. Every status is present, with zero when no employee has it.
/// </summary>
public sealed record SyncStatusReport
{
    public required int Pending { get; init; }

    public required int Synced { get; init; }

    public required int Failed { get; init; }

    public int Total => Pending + Synced + Failed;
}
