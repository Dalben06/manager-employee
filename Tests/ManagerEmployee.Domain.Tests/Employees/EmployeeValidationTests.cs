using ManagerEmployee.Domain.Employees;
using ManagerEmployee.Domain.Employees.Enums;
using ManagerEmployee.Domain.Employees.Policies;

namespace ManagerEmployee.Domain.Tests.Employees;

[TestClass]
public sealed class EmployeeValidationTests
{
    [TestMethod]
    public void IdentityCreate_WithUnsupportedProvider_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => EmployeeIdentity.Create((EmployeeProvider)999, "P1_001"));
    }

    [TestMethod]
    public void IdentityCreate_WithMissingId_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => EmployeeIdentity.Create(EmployeeProvider.Provider1, " "));
    }

    [TestMethod]
    public void IdentityCreate_WithIdBeyondSharedLimit_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => EmployeeIdentity.Create(
            EmployeeProvider.Provider1,
            new string('x', EmployeeFieldLimits.ProviderEmployeeId + 1)));
    }

    [TestMethod]
    public void IdentityCreate_NormalizesProviderEmployeeId()
    {
        var identity = EmployeeIdentity.Create(EmployeeProvider.Provider1, " P1_001 ");

        Assert.AreEqual("P1_001", identity.ProviderEmployeeId);
    }

    [TestMethod]
    public void ProfileCreate_WithInvalidRequiredField_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => CreateProfile(firstName: " "));
    }

    [TestMethod]
    public void ProfileCreate_WithInvalidEmail_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => CreateProfile(email: "not-an-email"));
    }

    [TestMethod]
    public void ProfileCreate_WithUnsupportedStatus_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => CreateProfile(employmentStatus: (EmployeeStatus)999));
    }

    [TestMethod]
    public void ProfileCreate_NormalizesRequiredAndOptionalValues()
    {
        var profile = CreateProfile(
            firstName: " Alice ",
            phoneNumber: " ",
            position: " Officer ");

        Assert.AreEqual("Alice", profile.FirstName);
        Assert.IsNull(profile.PhoneNumber);
        Assert.AreEqual("Officer", profile.Position);
    }

    [TestMethod]
    public void AttemptCreate_WithMissingProfile_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => EmployeeSynchronizationAttempt.Create(null!, AttemptedAt));
    }

    [TestMethod]
    public void AttemptCreate_WithDefaultTimestamp_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => EmployeeSynchronizationAttempt.Create(CreateProfile(), default));
    }

    [TestMethod]
    public void SynchronizationErrorCreate_NormalizesAndTruncatesValue()
    {
        var value = $"  {new string('x', EmployeeFieldLimits.LastError + 10)}  ";

        var error = SynchronizationError.Create(value);

        Assert.AreEqual(EmployeeFieldLimits.LastError, error.Value.Length);
        Assert.IsFalse(error.Value.StartsWith(' '));
    }

    private static readonly DateTimeOffset AttemptedAt =
        new(2026, 11, 11, 12, 30, 0, TimeSpan.Zero);

    private static EmployeeProfile CreateProfile(
        string firstName = "Alice",
        string email = "alice.johnson@provider1.com",
        string? phoneNumber = null,
        string? position = null,
        EmployeeStatus employmentStatus = EmployeeStatus.Active) =>
        EmployeeProfile.Create(
            firstName,
            "Johnson",
            email,
            phoneNumber,
            position,
            null,
            null,
            employmentStatus);
}
