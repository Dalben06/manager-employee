using ManagerEmployee.Application.Employees.Models;

namespace ManagerEmployee.Api.Contracts;

/// <summary>
/// Stored employee counts per synchronization status.
/// </summary>
public sealed record SyncStatusReportResponse
{
    public required int Pending { get; init; }

    public required int Synced { get; init; }

    public required int Failed { get; init; }

    public required int Total { get; init; }

    public static SyncStatusReportResponse From(SyncStatusReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return new SyncStatusReportResponse
        {
            Pending = report.Pending,
            Synced = report.Synced,
            Failed = report.Failed,
            Total = report.Total,
        };
    }
}
