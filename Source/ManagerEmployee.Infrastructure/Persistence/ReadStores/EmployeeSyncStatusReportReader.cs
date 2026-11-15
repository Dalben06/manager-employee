using ManagerEmployee.Application.Employees.Ports;
using ManagerEmployee.Domain.Employees.Enums;
using Microsoft.EntityFrameworkCore;

namespace ManagerEmployee.Infrastructure.Persistence.ReadStores;

/// <summary>
/// Reads employee counts per synchronization status from the <c>dbo.usp_GetEmployeeSyncStatusCounts</c>
/// stored procedure created by the <c>AddEmployeeSyncStatusReport</c> migration.
/// </summary>
internal sealed class EmployeeSyncStatusReportReader(ManagerEmployeeDbContext dbContext)
    : IEmployeeSyncStatusReportReader
{
    public async Task<IReadOnlyDictionary<SynchronizationStatus, int>> CountBySyncStatusAsync(
        CancellationToken cancellationToken)
    {
        // EXEC is not composable, so no LINQ operator may follow SqlQuery; rows are materialized as returned.
        var rows = await dbContext.Database
            .SqlQuery<SyncStatusCountRow>($"EXEC dbo.usp_GetEmployeeSyncStatusCounts")
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            row => SynchronizationStatusExtensions.TryParseContractValue(row.SyncStatus, out var status)
                ? status
                : throw new InvalidOperationException(
                    $"Unsupported synchronization status value '{row.SyncStatus}'."),
            row => row.EmployeeCount);
    }

    private sealed class SyncStatusCountRow
    {
        public string SyncStatus { get; init; } = string.Empty;

        public int EmployeeCount { get; init; }
    }
}
