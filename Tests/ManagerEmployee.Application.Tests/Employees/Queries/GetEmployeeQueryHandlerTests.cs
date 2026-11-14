using ManagerEmployee.Application.Employees.Models;
using ManagerEmployee.Application.Employees.Ports;
using ManagerEmployee.Application.Employees.Queries.GetEmployee;
using ManagerEmployee.Domain.Employees.Enums;

namespace ManagerEmployee.Application.Tests.Employees.Queries;

[TestClass]
public sealed class GetEmployeeQueryHandlerTests
{
    [TestMethod]
    public async Task HandleAsync_ExistingIdentity_ReturnsImmutableDetails()
    {
        var expected = EmployeeDetails(EmployeeProvider.Provider1, "P1_001");
        var readStore = new FakeEmployeeReadStore(expected);
        var handler = new GetEmployeeQueryHandler(readStore);

        var result = await handler.HandleAsync(
            new GetEmployeeQuery(EmployeeProvider.Provider1, " P1_001 "),
            CancellationToken.None);

        Assert.AreSame(expected, result);
        Assert.AreEqual(EmployeeProvider.Provider1, readStore.Provider);
        Assert.AreEqual("P1_001", readStore.ProviderEmployeeId);
    }

    [TestMethod]
    public async Task HandleAsync_MissingIdentity_ReturnsNull()
    {
        var handler = new GetEmployeeQueryHandler(new FakeEmployeeReadStore(null));

        var result = await handler.HandleAsync(
            new GetEmployeeQuery(EmployeeProvider.Provider2, "P2_404"),
            CancellationToken.None);

        Assert.IsNull(result);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public async Task HandleAsync_BlankIdentifier_ReturnsNullWithoutQueryingStore(string providerEmployeeId)
    {
        var readStore = new FakeEmployeeReadStore(null);
        var handler = new GetEmployeeQueryHandler(readStore);

        var result = await handler.HandleAsync(
            new GetEmployeeQuery(EmployeeProvider.Provider1, providerEmployeeId),
            CancellationToken.None);

        Assert.IsNull(result);
        Assert.IsNull(readStore.ProviderEmployeeId);
    }

    [TestMethod]
    public void EmployeeDetails_SameEmailAcrossProviders_PreservesDistinctIdentities()
    {
        var provider1 = EmployeeDetails(EmployeeProvider.Provider1, "SHARED");
        var provider2 = EmployeeDetails(EmployeeProvider.Provider2, "SHARED");

        Assert.AreEqual(provider1.Email, provider2.Email);
        Assert.AreNotEqual(provider1.Provider, provider2.Provider);
        Assert.AreNotEqual(provider1.TrackTikEmployeeId, provider2.TrackTikEmployeeId);
    }

    private static EmployeeDetails EmployeeDetails(
        EmployeeProvider provider,
        string providerEmployeeId) => new()
        {
            Provider = provider,
            ProviderEmployeeId = providerEmployeeId,
            TrackTikEmployeeId = $"{provider.ToContractValue()}-{providerEmployeeId}",
            FirstName = "Alex",
            LastName = "Smith",
            Email = "shared@example.com",
            EmploymentStatus = EmployeeStatus.Active,
            SyncStatus = SynchronizationStatus.Synced,
            Attempts = 1,
        };

    private sealed class FakeEmployeeReadStore(EmployeeDetails? result) : IEmployeeReadStore
    {
        public EmployeeProvider? Provider { get; private set; }

        public string? ProviderEmployeeId { get; private set; }

        public Task<EmployeeDetails?> FindAsync(
            EmployeeProvider provider,
            string providerEmployeeId,
            CancellationToken cancellationToken)
        {
            Provider = provider;
            ProviderEmployeeId = providerEmployeeId;
            return Task.FromResult(result);
        }
    }
}
