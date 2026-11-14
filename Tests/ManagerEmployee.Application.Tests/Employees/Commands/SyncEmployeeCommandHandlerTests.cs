using System.Text.Json;
using ManagerEmployee.Application.Employees.Commands.SyncEmployee;
using ManagerEmployee.Application.Events;
using ManagerEmployee.Application.Providers;
using ManagerEmployee.Application.Synchronization;
using ManagerEmployee.Domain.Employees;
using ManagerEmployee.Domain.Employees.Enums;
using ManagerEmployee.Domain.Employees.Events;
using ManagerEmployee.Domain.Employees.Repositories;

namespace ManagerEmployee.Application.Tests.Employees.Commands;

[TestClass]
public sealed class SyncEmployeeCommandHandlerTests
{
    private static readonly DateTimeOffset AttemptedAt = new(2026, 11, 11, 12, 30, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task HandleAsync_NewEmployee_CommitsPendingCreatesCommitsSyncedThenPublishesOnce()
    {
        var operations = new List<string>();
        var repository = new FakeEmployeeRepository(operations);
        var trackTikClient = new FakeTrackTikEmployeeClient(operations, TrackTikSyncResult.Success());
        var publisher = new FakeEventPublisher(operations);
        using var coordinator = new EmployeeSynchronizationCoordinator();
        var handler = CreateHandler(repository, trackTikClient, publisher, coordinator);

        var result = await handler.HandleAsync(Command(), CancellationToken.None);

        Assert.AreEqual(SyncEmployeeOutcome.Synchronized, result.Outcome);
        Assert.IsNotNull(result.Employee);
        Assert.AreEqual("provider1-P1_001", result.Employee.TrackTikEmployeeId);
        Assert.IsTrue(result.EmployeeCreated);
        CollectionAssert.AreEqual(
            new[] { "find", "add", "commit:Pending", "create", "commit:Synced", "publish" },
            operations);
        Assert.HasCount(1, publisher.Events);
        Assert.AreEqual("P1_001", publisher.Events[0].ProviderEmployeeId);
    }

    [TestMethod]
    public async Task HandleAsync_PreviouslySynchronizedEmployee_UsesUpdate()
    {
        var operations = new List<string>();
        var existing = CreateEmployee();
        existing.MarkSynced(AttemptedAt);
        var repository = new FakeEmployeeRepository(operations, existing);
        var trackTikClient = new FakeTrackTikEmployeeClient(operations, TrackTikSyncResult.Success());
        var publisher = new FakeEventPublisher(operations);
        using var coordinator = new EmployeeSynchronizationCoordinator();
        var handler = CreateHandler(repository, trackTikClient, publisher, coordinator);

        var result = await handler.HandleAsync(Command(), CancellationToken.None);

        Assert.AreEqual(SyncEmployeeOutcome.Synchronized, result.Outcome);
        CollectionAssert.AreEqual(
            new[] { "find", "commit:Pending", "update", "commit:Synced", "publish" },
            operations);
        Assert.AreEqual(2, existing.Attempts);
        Assert.IsFalse(result.EmployeeCreated);
    }

    [TestMethod]
    public async Task HandleAsync_PreviouslyFailedEmployee_RetriesWithCreate()
    {
        var operations = new List<string>();
        var existing = CreateEmployee();
        existing.MarkFailed(SynchronizationError.Create("TrackTik unavailable."));
        var repository = new FakeEmployeeRepository(operations, existing);
        var trackTikClient = new FakeTrackTikEmployeeClient(operations, TrackTikSyncResult.Success());
        var publisher = new FakeEventPublisher(operations);
        using var coordinator = new EmployeeSynchronizationCoordinator();
        var handler = CreateHandler(repository, trackTikClient, publisher, coordinator);

        var result = await handler.HandleAsync(Command(), CancellationToken.None);

        Assert.AreEqual(SyncEmployeeOutcome.Synchronized, result.Outcome);
        Assert.Contains("create", operations);
        Assert.DoesNotContain("update", operations);
        Assert.AreEqual(2, existing.Attempts);
    }

    [TestMethod]
    public async Task HandleAsync_TrackTikFailure_CommitsFailedAndDoesNotPublish()
    {
        var operations = new List<string>();
        var repository = new FakeEmployeeRepository(operations);
        var trackTikClient = new FakeTrackTikEmployeeClient(
            operations,
            TrackTikSyncResult.Failure("TrackTik returned 500."));
        var publisher = new FakeEventPublisher(operations);
        using var coordinator = new EmployeeSynchronizationCoordinator();
        var handler = CreateHandler(repository, trackTikClient, publisher, coordinator);

        var result = await handler.HandleAsync(Command(), CancellationToken.None);

        Assert.AreEqual(SyncEmployeeOutcome.SynchronizationFailed, result.Outcome);
        Assert.IsNotNull(result.Employee);
        Assert.AreEqual(SynchronizationStatus.Failed, result.Employee.SyncStatus);
        Assert.AreEqual("TrackTik returned 500.", result.Employee.LastError);
        CollectionAssert.AreEqual(
            new[] { "find", "add", "commit:Pending", "create", "commit:Failed" },
            operations);
        Assert.IsEmpty(publisher.Events);
    }

    [TestMethod]
    public async Task HandleAsync_ValidationFailure_PersistsAndForwardsNothing()
    {
        var operations = new List<string>();
        var repository = new FakeEmployeeRepository(operations);
        var trackTikClient = new FakeTrackTikEmployeeClient(operations, TrackTikSyncResult.Success());
        var publisher = new FakeEventPublisher(operations);
        var mapper = new FakeProviderMapper(ProviderEmployeeMappingResult.Failure([
            new ProviderValidationError("email_address", "email_address is invalid."),
        ]));
        using var coordinator = new EmployeeSynchronizationCoordinator();
        var handler = CreateHandler(repository, trackTikClient, publisher, coordinator, mapper);

        var result = await handler.HandleAsync(Command(), CancellationToken.None);

        Assert.AreEqual(SyncEmployeeOutcome.ValidationFailed, result.Outcome);
        Assert.IsEmpty(operations);
        Assert.IsEmpty(publisher.Events);
    }

    [TestMethod]
    public async Task HandleAsync_PendingCommitConflict_DoesNotForwardOrPublish()
    {
        var operations = new List<string>();
        var repository = new FakeEmployeeRepository(operations);
        repository.CommitOutcomes.Enqueue(EmployeeCommitOutcome.Conflict);
        var trackTikClient = new FakeTrackTikEmployeeClient(operations, TrackTikSyncResult.Success());
        var publisher = new FakeEventPublisher(operations);
        using var coordinator = new EmployeeSynchronizationCoordinator();
        var handler = CreateHandler(repository, trackTikClient, publisher, coordinator);

        var result = await handler.HandleAsync(Command(), CancellationToken.None);

        Assert.AreEqual(SyncEmployeeOutcome.PersistenceConflict, result.Outcome);
        Assert.DoesNotContain("create", operations);
        Assert.IsEmpty(publisher.Events);
    }

    [TestMethod]
    public async Task HandleAsync_SyncedCommitConflict_DoesNotPublish()
    {
        var operations = new List<string>();
        var repository = new FakeEmployeeRepository(operations);
        repository.CommitOutcomes.Enqueue(EmployeeCommitOutcome.Saved);
        repository.CommitOutcomes.Enqueue(EmployeeCommitOutcome.Conflict);
        var trackTikClient = new FakeTrackTikEmployeeClient(operations, TrackTikSyncResult.Success());
        var publisher = new FakeEventPublisher(operations);
        using var coordinator = new EmployeeSynchronizationCoordinator();
        var handler = CreateHandler(repository, trackTikClient, publisher, coordinator);

        var result = await handler.HandleAsync(Command(), CancellationToken.None);

        Assert.AreEqual(SyncEmployeeOutcome.PersistenceConflict, result.Outcome);
        Assert.IsEmpty(publisher.Events);
        Assert.DoesNotContain("publish", operations);
    }

    [TestMethod]
    public async Task HandleAsync_UnexpectedFinalCommitFailure_DoesNotPublish()
    {
        var operations = new List<string>();
        var repository = new FakeEmployeeRepository(operations) { ThrowOnCommitNumber = 2 };
        var trackTikClient = new FakeTrackTikEmployeeClient(operations, TrackTikSyncResult.Success());
        var publisher = new FakeEventPublisher(operations);
        using var coordinator = new EmployeeSynchronizationCoordinator();
        var handler = CreateHandler(repository, trackTikClient, publisher, coordinator);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => handler.HandleAsync(Command(), CancellationToken.None));

        Assert.IsEmpty(publisher.Events);
        Assert.DoesNotContain("publish", operations);
    }

    [TestMethod]
    public async Task HandleAsync_RequestCancelledDuringTrackTikCall_StillCommitsSyncedAndPublishes()
    {
        using var request = new CancellationTokenSource();
        var operations = new List<string>();
        var repository = new FakeEmployeeRepository(operations);
        var trackTikClient = new FakeTrackTikEmployeeClient(operations, TrackTikSyncResult.Success())
        {
            OnCall = request.Cancel,
        };
        var publisher = new FakeEventPublisher(operations);
        using var coordinator = new EmployeeSynchronizationCoordinator();
        var handler = CreateHandler(repository, trackTikClient, publisher, coordinator);

        var result = await handler.HandleAsync(Command(), request.Token);

        Assert.AreEqual(SyncEmployeeOutcome.Synchronized, result.Outcome);
        CollectionAssert.AreEqual(
            new[] { "find", "add", "commit:Pending", "create", "commit:Synced", "publish" },
            operations);
        Assert.HasCount(1, publisher.Events);
    }

    [TestMethod]
    public async Task HandleAsync_UnknownProvider_ReturnsWithoutUsingCollaborators()
    {
        var operations = new List<string>();
        var repository = new FakeEmployeeRepository(operations);
        var trackTikClient = new FakeTrackTikEmployeeClient(operations, TrackTikSyncResult.Success());
        var publisher = new FakeEventPublisher(operations);
        using var coordinator = new EmployeeSynchronizationCoordinator();
        var handler = CreateHandler(repository, trackTikClient, publisher, coordinator);

        var result = await handler.HandleAsync(
            new SyncEmployeeCommand("unknown", default),
            CancellationToken.None);

        Assert.AreEqual(SyncEmployeeOutcome.UnknownProvider, result.Outcome);
        Assert.IsEmpty(operations);
    }

    private static SyncEmployeeCommandHandler CreateHandler(
        IEmployeeRepository repository,
        ITrackTikEmployeeClient trackTikClient,
        IEmployeeSyncedEventPublisher publisher,
        EmployeeSynchronizationCoordinator coordinator,
        IProviderEmployeeMapper? mapper = null)
    {
        mapper ??= new FakeProviderMapper(ProviderEmployeeMappingResult.Success(MappedEmployee()));
        return new SyncEmployeeCommandHandler(
            [mapper],
            repository,
            trackTikClient,
            publisher,
            coordinator,
            new FixedTimeProvider(AttemptedAt));
    }

    private static SyncEmployeeCommand Command() => new("provider1", default);

    private static MappedEmployee MappedEmployee() => new()
    {
        Provider = EmployeeProvider.Provider1,
        ProviderEmployeeId = "P1_001",
        FirstName = "Alice",
        LastName = "Johnson",
        Email = "alice.johnson@provider1.com",
        PhoneNumber = "+1-555-0101",
        Position = "Security Officer",
        Department = "Security Operations",
        StartDate = new DateOnly(2024, 1, 15),
        EmploymentStatus = EmployeeStatus.Active,
    };

    private static Employee CreateEmployee() => Employee.CreateForSynchronization(
        EmployeeIdentity.Create(EmployeeProvider.Provider1, "P1_001"),
        EmployeeSynchronizationAttempt.Create(
            EmployeeProfile.Create(
                "Alice",
                "Johnson",
                "alice.johnson@provider1.com",
                "+1-555-0101",
                "Security Officer",
                "Security Operations",
                new DateOnly(2024, 1, 15),
                EmployeeStatus.Active),
            AttemptedAt));

    private sealed class FakeProviderMapper(ProviderEmployeeMappingResult result) : IProviderEmployeeMapper
    {
        public EmployeeProvider Provider => EmployeeProvider.Provider1;

        public ProviderEmployeeMappingResult Map(JsonElement payload) => result;
    }

    private sealed class FakeEmployeeRepository(
        List<string> operations,
        Employee? existingEmployee = null) : IEmployeeRepository
    {
        private int _commitCount;
        private Employee? _employee = existingEmployee;

        public Queue<EmployeeCommitOutcome> CommitOutcomes { get; } = new();

        public int? ThrowOnCommitNumber { get; init; }

        public Task<Employee?> FindByIdentityAsync(
            EmployeeProvider provider,
            string providerEmployeeId,
            CancellationToken cancellationToken)
        {
            operations.Add("find");
            return Task.FromResult(_employee);
        }

        public Task AddAsync(Employee employee, CancellationToken cancellationToken)
        {
            _employee = employee;
            operations.Add("add");
            return Task.CompletedTask;
        }

        public Task<EmployeeCommitOutcome> CommitAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _commitCount++;
            operations.Add($"commit:{_employee?.SyncStatus}");
            if (_commitCount == ThrowOnCommitNumber)
            {
                throw new InvalidOperationException("Commit failed.");
            }

            return Task.FromResult(
                CommitOutcomes.TryDequeue(out var outcome)
                    ? outcome
                    : EmployeeCommitOutcome.Saved);
        }
    }

    private sealed class FakeTrackTikEmployeeClient(
        List<string> operations,
        TrackTikSyncResult result) : ITrackTikEmployeeClient
    {
        /// <summary>Runs when TrackTik is called, for example to simulate the caller disconnecting.</summary>
        public Action? OnCall { get; init; }

        public Task<TrackTikSyncResult> CreateAsync(
            TrackTikEmployee employee,
            CancellationToken cancellationToken)
        {
            operations.Add("create");
            OnCall?.Invoke();
            return Task.FromResult(result);
        }

        public Task<TrackTikSyncResult> UpdateAsync(
            TrackTikEmployee employee,
            CancellationToken cancellationToken)
        {
            operations.Add("update");
            OnCall?.Invoke();
            return Task.FromResult(result);
        }
    }

    private sealed class FakeEventPublisher(List<string> operations) : IEmployeeSyncedEventPublisher
    {
        public List<EmployeeSynced> Events { get; } = [];

        public Task PublishAsync(
            EmployeeSynced notification,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            operations.Add("publish");
            Events.Add(notification);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
