using ManagerEmployee.Domain.Employees;
using ManagerEmployee.Domain.Employees.Enums;
using ManagerEmployee.Domain.Employees.Policies;

namespace ManagerEmployee.Domain.Tests.Employees;

[TestClass]
public sealed class EmployeeTests
{
    private static readonly DateTimeOffset AttemptedAt = new(2026, 11, 11, 12, 30, 0, TimeSpan.Zero);

    [TestMethod]
    public void CreateForSynchronization_WithValidValues_CreatesPendingEmployee()
    {
        var employee = CreateEmployee();

        Assert.AreEqual(EmployeeProvider.Provider1, employee.Provider);
        Assert.AreEqual("P1_001", employee.ProviderEmployeeId);
        Assert.AreEqual(SynchronizationStatus.Pending, employee.SyncStatus);
        Assert.AreEqual(1, employee.Attempts);
        Assert.AreEqual(AttemptedAt, employee.LastAttemptAt);
        Assert.IsFalse(employee.HasBeenCreatedInTrackTik);
        Assert.IsNull(employee.LastError);
    }

    [TestMethod]
    public void CreateForSynchronization_WithNullIdentity_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => Employee.CreateForSynchronization(null!, Attempt()));
    }

    [TestMethod]
    public void CreateForSynchronization_WithNullAttempt_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => Employee.CreateForSynchronization(Identity(), null!));
    }

    [TestMethod]
    public void PrepareResubmission_UpdatesDetailsAndRecordsAnotherPendingAttempt()
    {
        var employee = CreateEmployee();
        employee.MarkFailed(Error("TrackTik was unavailable."));
        var nextAttemptAt = AttemptedAt.AddMinutes(5);
        var updatedProfile = Profile(
            lastName: "Johnson-Smith",
            position: "Senior Security Officer",
            employmentStatus: EmployeeStatus.Inactive);

        employee.PrepareResubmission(Attempt(updatedProfile, nextAttemptAt));

        Assert.AreEqual("Johnson-Smith", employee.LastName);
        Assert.AreEqual("Senior Security Officer", employee.Position);
        Assert.AreEqual(EmployeeStatus.Inactive, employee.EmploymentStatus);
        Assert.AreEqual(SynchronizationStatus.Pending, employee.SyncStatus);
        Assert.AreEqual(2, employee.Attempts);
        Assert.AreEqual(nextAttemptAt, employee.LastAttemptAt);
    }

    [TestMethod]
    public void MarkSynced_ClearsPreviousErrorAndRemembersSuccessfulSynchronization()
    {
        var employee = CreateEmployee();
        employee.MarkFailed(Error("TrackTik was unavailable."));
        employee.PrepareResubmission(Attempt(attemptedAt: AttemptedAt.AddMinutes(5)));

        var occurredAt = AttemptedAt.AddMinutes(6);
        var notification = employee.MarkSynced(occurredAt);

        Assert.AreEqual(SynchronizationStatus.Synced, employee.SyncStatus);
        Assert.IsTrue(employee.HasBeenCreatedInTrackTik);
        Assert.IsNull(employee.LastError);
        Assert.AreEqual(EmployeeProvider.Provider1, notification.Provider);
        Assert.AreEqual("P1_001", notification.ProviderEmployeeId);
        Assert.AreEqual(occurredAt, notification.OccurredAt);
    }

    [TestMethod]
    public void FailedRetryAfterSuccess_PreservesPriorSuccessfulSynchronization()
    {
        var employee = CreateEmployee();
        employee.MarkSynced(AttemptedAt.AddMinutes(1));
        employee.PrepareResubmission(Attempt(attemptedAt: AttemptedAt.AddMinutes(5)));

        employee.MarkFailed(Error("TrackTik was unavailable."));

        Assert.AreEqual(SynchronizationStatus.Failed, employee.SyncStatus);
        Assert.IsTrue(employee.HasBeenCreatedInTrackTik);
    }

    [TestMethod]
    public void CompletingAnAlreadyCompletedAttempt_Throws()
    {
        var employee = CreateEmployee();
        employee.MarkSynced(AttemptedAt.AddMinutes(1));

        Assert.ThrowsExactly<InvalidOperationException>(
            () => employee.MarkFailed(Error("Unexpected failure.")));
    }

    [TestMethod]
    public void InvalidResubmissionValue_DoesNotPartiallyMutateEmployee()
    {
        var employee = CreateEmployee();
        var originalFirstName = employee.FirstName;
        var originalAttempts = employee.Attempts;
        var originalAttemptedAt = employee.LastAttemptAt;

        Assert.ThrowsExactly<ArgumentException>(() => Profile(email: "invalid-email"));

        Assert.AreEqual(originalFirstName, employee.FirstName);
        Assert.AreEqual(originalAttempts, employee.Attempts);
        Assert.AreEqual(originalAttemptedAt, employee.LastAttemptAt);
        Assert.AreEqual(SynchronizationStatus.Pending, employee.SyncStatus);
    }

    [TestMethod]
    public void MarkFailed_WithConstrainedError_PreservesFailureTransition()
    {
        var employee = CreateEmployee();
        var error = Error(new string('x', EmployeeFieldLimits.LastError + 10));

        employee.MarkFailed(error);

        Assert.AreEqual(SynchronizationStatus.Failed, employee.SyncStatus);
        Assert.AreEqual(EmployeeFieldLimits.LastError, employee.LastError?.Length);
    }

    [TestMethod]
    public void MarkSynced_WithMissingOccurrenceTime_DoesNotMutateEmployee()
    {
        var employee = CreateEmployee();

        Assert.ThrowsExactly<ArgumentException>(() => employee.MarkSynced(default));

        Assert.AreEqual(SynchronizationStatus.Pending, employee.SyncStatus);
        Assert.IsFalse(employee.HasBeenCreatedInTrackTik);
    }

    private static Employee CreateEmployee() =>
        Employee.CreateForSynchronization(Identity(), Attempt());

    private static EmployeeIdentity Identity() =>
        EmployeeIdentity.Create(EmployeeProvider.Provider1, "P1_001");

    private static EmployeeProfile Profile(
        string firstName = "Alice",
        string lastName = "Johnson",
        string email = "alice.johnson@provider1.com",
        string? position = "Security Officer",
        EmployeeStatus employmentStatus = EmployeeStatus.Active) =>
        EmployeeProfile.Create(
            firstName,
            lastName,
            email,
            "+1-555-0101",
            position,
            "Security Operations",
            new DateOnly(2024, 1, 15),
            employmentStatus);

    private static EmployeeSynchronizationAttempt Attempt(
        EmployeeProfile? profile = null,
        DateTimeOffset? attemptedAt = null) =>
        EmployeeSynchronizationAttempt.Create(profile ?? Profile(), attemptedAt ?? AttemptedAt);

    private static SynchronizationError Error(string value) =>
        SynchronizationError.Create(value);
}
