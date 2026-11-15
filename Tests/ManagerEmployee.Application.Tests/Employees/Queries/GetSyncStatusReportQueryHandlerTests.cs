using ManagerEmployee.Application.Employees.Ports;
using ManagerEmployee.Application.Employees.Queries.GetSyncStatusReport;
using ManagerEmployee.Domain.Employees.Enums;

namespace ManagerEmployee.Application.Tests.Employees.Queries;

[TestClass]
public sealed class GetSyncStatusReportQueryHandlerTests
{
    [TestMethod]
    public async Task HandleAsync_AllStatusesPresent_ReturnsCountsAndTotal()
    {
        var handler = new GetSyncStatusReportQueryHandler(new FakeReportReader(new()
        {
            [SynchronizationStatus.Pending] = 1,
            [SynchronizationStatus.Synced] = 4,
            [SynchronizationStatus.Failed] = 2,
        }));

        var report = await handler.HandleAsync(CancellationToken.None);

        Assert.AreEqual(1, report.Pending);
        Assert.AreEqual(4, report.Synced);
        Assert.AreEqual(2, report.Failed);
        Assert.AreEqual(7, report.Total);
    }

    [TestMethod]
    public async Task HandleAsync_NoEmployees_ReturnsZeroForEveryStatus()
    {
        var handler = new GetSyncStatusReportQueryHandler(new FakeReportReader([]));

        var report = await handler.HandleAsync(CancellationToken.None);

        Assert.AreEqual(0, report.Pending);
        Assert.AreEqual(0, report.Synced);
        Assert.AreEqual(0, report.Failed);
        Assert.AreEqual(0, report.Total);
    }

    private sealed class FakeReportReader(Dictionary<SynchronizationStatus, int> counts)
        : IEmployeeSyncStatusReportReader
    {
        public Task<IReadOnlyDictionary<SynchronizationStatus, int>> CountBySyncStatusAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<SynchronizationStatus, int>>(counts);
    }
}
