using ManagerEmployee.Application.Employees.Models;
using ManagerEmployee.Application.Employees.Ports;
using ManagerEmployee.Domain.Employees.Enums;

namespace ManagerEmployee.Application.Employees.Queries.GetSyncStatusReport;

/// <summary>
/// Reports stored employee counts per synchronization status. The query has no parameters, so no query type is defined.
/// </summary>
public sealed class GetSyncStatusReportQueryHandler(IEmployeeSyncStatusReportReader reportReader)
{
    public async Task<SyncStatusReport> HandleAsync(CancellationToken cancellationToken)
    {
        var counts = await reportReader.CountBySyncStatusAsync(cancellationToken);

        return new SyncStatusReport
        {
            Pending = counts.GetValueOrDefault(SynchronizationStatus.Pending),
            Synced = counts.GetValueOrDefault(SynchronizationStatus.Synced),
            Failed = counts.GetValueOrDefault(SynchronizationStatus.Failed),
        };
    }
}
