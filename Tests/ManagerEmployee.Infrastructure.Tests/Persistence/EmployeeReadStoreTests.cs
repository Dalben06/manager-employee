using ManagerEmployee.Application.Employees.Ports;
using ManagerEmployee.Domain.Employees;
using ManagerEmployee.Domain.Employees.Enums;
using ManagerEmployee.Infrastructure.Persistence;
using ManagerEmployee.Infrastructure.Persistence.ReadStores;
using Microsoft.EntityFrameworkCore;

namespace ManagerEmployee.Infrastructure.Tests.Persistence;

[TestClass]
public sealed class EmployeeReadStoreTests
{
    private static readonly DateTimeOffset AttemptedAt = new(2026, 11, 11, 12, 30, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task FindAsync_ProjectsAllRequiredDetailsWithoutTrackingAggregate()
    {
        await using var dbContext = CreateDbContext();
        var employee = CreateEmployee(EmployeeProvider.Provider1, "P1_001", "shared@example.com");
        employee.MarkFailed(SynchronizationError.Create("TrackTik unavailable."));
        var entry = dbContext.Employees.Add(employee);
        entry.Property<byte[]>("RowVersion").CurrentValue = [1];
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        IEmployeeReadStore readStore = new EmployeeReadStore(dbContext);
        var result = await readStore.FindAsync(
            EmployeeProvider.Provider1,
            "P1_001",
            CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.AreEqual("provider1-P1_001", result.TrackTikEmployeeId);
        Assert.AreEqual(SynchronizationStatus.Failed, result.SyncStatus);
        Assert.AreEqual(1, result.Attempts);
        Assert.AreEqual("TrackTik unavailable.", result.LastError);
        Assert.AreEqual(AttemptedAt, result.LastAttemptAt);
        Assert.IsEmpty(dbContext.ChangeTracker.Entries());
    }

    [TestMethod]
    public async Task FindAsync_SameEmailAcrossProviders_ReturnsIndependentEmployees()
    {
        await using var dbContext = CreateDbContext();
        var provider1Employee = CreateEmployee(
            EmployeeProvider.Provider1,
            "SHARED",
            "shared@example.com");
        var provider2Employee = CreateEmployee(
            EmployeeProvider.Provider2,
            "SHARED",
            "shared@example.com");
        dbContext.Employees.AddRange(provider1Employee, provider2Employee);
        dbContext.Entry(provider1Employee).Property<byte[]>("RowVersion").CurrentValue = [1];
        dbContext.Entry(provider2Employee).Property<byte[]>("RowVersion").CurrentValue = [2];
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        IEmployeeReadStore readStore = new EmployeeReadStore(dbContext);

        var provider1 = await readStore.FindAsync(
            EmployeeProvider.Provider1,
            "SHARED",
            CancellationToken.None);
        var provider2 = await readStore.FindAsync(
            EmployeeProvider.Provider2,
            "SHARED",
            CancellationToken.None);

        Assert.IsNotNull(provider1);
        Assert.IsNotNull(provider2);
        Assert.AreEqual(provider1.Email, provider2.Email);
        Assert.AreNotEqual(provider1.Provider, provider2.Provider);
        Assert.AreNotEqual(provider1.TrackTikEmployeeId, provider2.TrackTikEmployeeId);
    }

    private static ManagerEmployeeDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<ManagerEmployeeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Employee CreateEmployee(
        EmployeeProvider provider,
        string providerEmployeeId,
        string email) => Employee.CreateForSynchronization(
            EmployeeIdentity.Create(provider, providerEmployeeId),
            EmployeeSynchronizationAttempt.Create(
                EmployeeProfile.Create(
                    "Alex",
                    "Smith",
                    email,
                    null,
                    null,
                    null,
                    null,
                    EmployeeStatus.Active),
                AttemptedAt));
}
