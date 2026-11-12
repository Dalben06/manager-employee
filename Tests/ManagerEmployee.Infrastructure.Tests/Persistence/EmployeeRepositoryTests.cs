using ManagerEmployee.Domain.Employees;
using ManagerEmployee.Domain.Employees.Enums;
using ManagerEmployee.Domain.Employees.Repositories;
using ManagerEmployee.Infrastructure.Persistence;
using ManagerEmployee.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ManagerEmployee.Infrastructure.Tests.Persistence;

[TestClass]
public sealed class EmployeeRepositoryTests
{
    [TestMethod]
    public async Task CommitAsync_OptimisticConcurrencyFailure_ReturnsConflict()
    {
        var options = new DbContextOptionsBuilder<ManagerEmployeeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new ThrowingConcurrencyInterceptor())
            .Options;
        await using var dbContext = new ManagerEmployeeDbContext(options);
        var repository = new EmployeeRepository(dbContext);
        await repository.AddAsync(CreateEmployee(), CancellationToken.None);

        var outcome = await repository.CommitAsync(CancellationToken.None);

        Assert.AreEqual(EmployeeCommitOutcome.Conflict, outcome);
    }

    [TestMethod]
    [DataRow(2601, true)]
    [DataRow(2627, true)]
    [DataRow(547, false)]
    public void IsUniqueConstraintViolationNumber_RecognizesSqlServerDuplicateErrors(
        int number,
        bool expected)
    {
        Assert.AreEqual(
            expected,
            EmployeeRepository.IsUniqueConstraintViolationNumber(number));
    }

    private static Employee CreateEmployee() => Employee.CreateForSynchronization(
        EmployeeIdentity.Create(EmployeeProvider.Provider1, "P1_001"),
        EmployeeSynchronizationAttempt.Create(
            EmployeeProfile.Create(
                "Alice",
                "Johnson",
                "alice@example.com",
                null,
                null,
                null,
                null,
                EmployeeStatus.Active),
            new DateTimeOffset(2026, 11, 11, 12, 30, 0, TimeSpan.Zero)));

    private sealed class ThrowingConcurrencyInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<InterceptionResult<int>>(new DbUpdateConcurrencyException());
    }
}
