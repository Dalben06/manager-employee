using ManagerEmployee.Domain.Employees.Enums;

namespace ManagerEmployee.Application.Employees.Ports;

public interface IEmployeeSyncStatusReportReader
{
    /// <summary>
    /// Returns the number of stored employees per synchronization status. Statuses with no employees may be absent.
    /// </summary>
    Task<IReadOnlyDictionary<SynchronizationStatus, int>> CountBySyncStatusAsync(CancellationToken cancellationToken);
}
