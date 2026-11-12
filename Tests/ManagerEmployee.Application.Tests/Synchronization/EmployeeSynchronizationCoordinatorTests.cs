using ManagerEmployee.Application.Synchronization;
using ManagerEmployee.Domain.Employees.Enums;

namespace ManagerEmployee.Application.Tests.Synchronization;

[TestClass]
public sealed class EmployeeSynchronizationCoordinatorTests
{
    [TestMethod]
    public async Task AcquireAsync_SameIdentity_SerializesCallers()
    {
        using var coordinator = new EmployeeSynchronizationCoordinator();
        await using var first = await coordinator.AcquireAsync(
            EmployeeProvider.Provider1,
            "P1_001",
            CancellationToken.None);

        var secondTask = coordinator.AcquireAsync(
            EmployeeProvider.Provider1,
            "P1_001",
            CancellationToken.None).AsTask();

        Assert.IsFalse(secondTask.IsCompleted);
        await first.DisposeAsync();

        await using var second = await secondTask;
        Assert.IsTrue(secondTask.IsCompletedSuccessfully);
    }
}
