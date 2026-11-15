using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ManagerEmployee.Api.ContainerTests.Infrastructure;
using ManagerEmployee.Api.IntegrationTests.Infrastructure;
using ManagerEmployee.Application.Synchronization;
using ManagerEmployee.Domain.Employees;
using ManagerEmployee.Domain.Employees.Enums;
using ManagerEmployee.Domain.Employees.Repositories;
using ManagerEmployee.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ManagerEmployee.Api.ContainerTests;

/// <summary>
/// Runs the API against a real SQL Server (Testcontainers) to prove what the EF InMemory provider cannot:
/// migrations, the composite primary key, check constraints, rowversion concurrency, and the report procedure.
/// </summary>
[TestClass]
public sealed class SqlServerApiTests
{
    private const string Provider2SharingAliceEmail = """
        {
          "employee_number": "P2_777",
          "personal_info": {
            "given_name": "Alice",
            "family_name": "Johnson",
            "email": "alice.johnson@provider1.com"
          },
          "work_info": {
            "current_status": "employed"
          }
        }
        """;

    [TestMethod]
    public async Task Startup_AppliesEveryMigrationAndCreatesReportProcedure()
    {
        using var factory = new SqlServerApiFactory();
        await using var scope = factory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ManagerEmployeeDbContext>();

        var applied = await dbContext.Database.GetAppliedMigrationsAsync();
        var procedures = await dbContext.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.procedures WHERE [name] = 'usp_GetEmployeeSyncStatusCounts'")
            .SingleAsync();

        CollectionAssert.AreEqual(dbContext.Database.GetMigrations().ToArray(), applied.ToArray());
        Assert.AreEqual(1, procedures);
    }

    [TestMethod]
    public async Task SubmitThenGet_RoundTripsEveryFieldThroughSqlServer()
    {
        using var factory = new SqlServerApiFactory();
        using var client = factory.CreateClient();

        using var submitted = await PostAsync(client, "provider1", ProviderPayloads.Provider1Alice);
        using var response = await client.GetAsync(new Uri("/api/v1/employees/provider1/P1_001", UriKind.Relative));

        Assert.AreEqual(HttpStatusCode.Created, submitted.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual("provider1-P1_001", body.GetProperty("employeeId").GetString());
        Assert.AreEqual("Alice", body.GetProperty("firstName").GetString());
        Assert.AreEqual("Johnson", body.GetProperty("lastName").GetString());
        Assert.AreEqual("alice.johnson@provider1.com", body.GetProperty("email").GetString());
        Assert.AreEqual("+1-555-0101", body.GetProperty("phoneNumber").GetString());
        Assert.AreEqual("Security Officer", body.GetProperty("position").GetString());
        Assert.AreEqual("Security Operations", body.GetProperty("department").GetString());
        Assert.AreEqual("2024-01-15", body.GetProperty("startDate").GetString());
        Assert.AreEqual("active", body.GetProperty("status").GetString());
        Assert.AreEqual("synced", body.GetProperty("syncStatus").GetString());
        Assert.AreEqual(1, body.GetProperty("attempts").GetInt32());
        Assert.AreEqual(JsonValueKind.Null, body.GetProperty("lastError").ValueKind);
        Assert.AreNotEqual(JsonValueKind.Null, body.GetProperty("lastAttemptAt").ValueKind);
    }

    [TestMethod]
    public async Task Resubmission_UpdatesTheSingleStoredRowAndUsesPut()
    {
        using var factory = new SqlServerApiFactory();
        using var client = factory.CreateClient();

        using var first = await PostAsync(client, "provider1", ProviderPayloads.Provider1Alice);
        using var second = await PostAsync(client, "provider1", ProviderPayloads.Provider1Alice);

        Assert.AreEqual(HttpStatusCode.Created, first.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, second.StatusCode);
        CollectionAssert.AreEqual(
            new[] { "create:provider1-P1_001", "update:provider1-P1_001" },
            factory.TrackTik.Calls.ToArray());
        await using var scope = factory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ManagerEmployeeDbContext>();
        var stored = await dbContext.Employees.AsNoTracking().SingleAsync();
        Assert.AreEqual(2, stored.Attempts);
        Assert.AreEqual(SynchronizationStatus.Synced, stored.SyncStatus);
    }

    [TestMethod]
    public async Task TrackTikFailure_PersistsFailedStateAndReturns502()
    {
        using var factory = new SqlServerApiFactory();
        factory.TrackTik.Result = TrackTikSyncResult.Failure("TrackTik returned 503 ServiceUnavailable.");
        using var client = factory.CreateClient();

        using var response = await PostAsync(client, "provider1", ProviderPayloads.Provider1Alice);

        Assert.AreEqual(HttpStatusCode.BadGateway, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual("failed", body.GetProperty("syncStatus").GetString());
        await using var scope = factory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ManagerEmployeeDbContext>();
        var stored = await dbContext.Employees.AsNoTracking().SingleAsync();
        Assert.AreEqual(SynchronizationStatus.Failed, stored.SyncStatus);
        Assert.AreEqual("TrackTik returned 503 ServiceUnavailable.", stored.LastError);
        Assert.IsFalse(stored.HasBeenCreatedInTrackTik);
    }

    [TestMethod]
    public async Task SameEmailFromDifferentProviders_IsStoredAsTwoEmployees()
    {
        using var factory = new SqlServerApiFactory();
        using var client = factory.CreateClient();

        using var provider1 = await PostAsync(client, "provider1", ProviderPayloads.Provider1Alice);
        using var provider2 = await PostAsync(client, "provider2", Provider2SharingAliceEmail);

        Assert.AreEqual(HttpStatusCode.Created, provider1.StatusCode);
        Assert.AreEqual(HttpStatusCode.Created, provider2.StatusCode);
        await using var scope = factory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ManagerEmployeeDbContext>();
        Assert.AreEqual(2, await dbContext.Employees.CountAsync(employee => employee.Email == "alice.johnson@provider1.com"));
    }

    [TestMethod]
    public async Task ConcurrentInsertOfSameIdentity_PrimaryKeyRejectsTheSecondAsConflict()
    {
        using var factory = new SqlServerApiFactory();
        await using var firstScope = factory.CreateScope();
        await using var secondScope = factory.CreateScope();
        var firstRepository = firstScope.ServiceProvider.GetRequiredService<IEmployeeRepository>();
        var secondRepository = secondScope.ServiceProvider.GetRequiredService<IEmployeeRepository>();

        await firstRepository.AddAsync(CreateEmployee(), CancellationToken.None);
        await secondRepository.AddAsync(CreateEmployee(), CancellationToken.None);
        var firstOutcome = await firstRepository.CommitAsync(CancellationToken.None);
        var secondOutcome = await secondRepository.CommitAsync(CancellationToken.None);

        Assert.AreEqual(EmployeeCommitOutcome.Saved, firstOutcome);
        Assert.AreEqual(EmployeeCommitOutcome.Conflict, secondOutcome);
        await using var verifyScope = factory.CreateScope();
        var dbContext = verifyScope.ServiceProvider.GetRequiredService<ManagerEmployeeDbContext>();
        Assert.AreEqual(1, await dbContext.Employees.CountAsync());
    }

    [TestMethod]
    public async Task StaleRowVersion_SecondWriterGetsConflict()
    {
        using var factory = new SqlServerApiFactory();
        await using (var seedScope = factory.CreateScope())
        {
            var seedRepository = seedScope.ServiceProvider.GetRequiredService<IEmployeeRepository>();
            await seedRepository.AddAsync(CreateEmployee(), CancellationToken.None);
            Assert.AreEqual(EmployeeCommitOutcome.Saved, await seedRepository.CommitAsync(CancellationToken.None));
        }

        await using var firstScope = factory.CreateScope();
        await using var secondScope = factory.CreateScope();
        var firstRepository = firstScope.ServiceProvider.GetRequiredService<IEmployeeRepository>();
        var secondRepository = secondScope.ServiceProvider.GetRequiredService<IEmployeeRepository>();
        var firstCopy = await firstRepository.FindByIdentityAsync(EmployeeProvider.Provider1, "P1_001", CancellationToken.None);
        var secondCopy = await secondRepository.FindByIdentityAsync(EmployeeProvider.Provider1, "P1_001", CancellationToken.None);
        Assert.IsNotNull(firstCopy);
        Assert.IsNotNull(secondCopy);

        firstCopy.MarkFailed(SynchronizationError.Create("first writer"));
        secondCopy.MarkFailed(SynchronizationError.Create("second writer"));
        var firstOutcome = await firstRepository.CommitAsync(CancellationToken.None);
        var secondOutcome = await secondRepository.CommitAsync(CancellationToken.None);

        Assert.AreEqual(EmployeeCommitOutcome.Saved, firstOutcome);
        Assert.AreEqual(EmployeeCommitOutcome.Conflict, secondOutcome);
    }

    [TestMethod]
    public async Task UnsupportedSyncStatus_IsRejectedByTheCheckConstraint()
    {
        using var factory = new SqlServerApiFactory();
        await using var scope = factory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ManagerEmployeeDbContext>();

        var exception = await Assert.ThrowsAsync<SqlException>(() => dbContext.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO dbo.[Employees]
                ([Provider], [ProviderEmployeeId], [FirstName], [LastName], [Email], [EmploymentStatus], [SyncStatus], [Attempts])
            VALUES ('provider1', 'P1_BAD', 'Bad', 'Status', 'bad@example.com', 'active', 'retrying', 1)
            """));

        Assert.Contains("CK_Employees_SyncStatus", exception.Message);
    }

    [TestMethod]
    public async Task SyncStatusReport_ReturnsCountsFromTheStoredProcedure()
    {
        using var factory = new SqlServerApiFactory();
        using var client = factory.CreateClient();
        using var alice = await PostAsync(client, "provider1", ProviderPayloads.Provider1Alice);
        using var carol = await PostAsync(client, "provider2", ProviderPayloads.Provider2Carol);
        factory.TrackTik.Result = TrackTikSyncResult.Failure("TrackTik returned 503 ServiceUnavailable.");
        using var failed = await PostAsync(client, "provider2", Provider2SharingAliceEmail);

        using var response = await client.GetAsync(new Uri("/api/v1/reports/sync-status", UriKind.Relative));

        Assert.AreEqual(HttpStatusCode.BadGateway, failed.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual(0, body.GetProperty("pending").GetInt32());
        Assert.AreEqual(2, body.GetProperty("synced").GetInt32());
        Assert.AreEqual(1, body.GetProperty("failed").GetInt32());
        Assert.AreEqual(3, body.GetProperty("total").GetInt32());
    }

    private static Employee CreateEmployee() => Employee.CreateForSynchronization(
        EmployeeIdentity.Create(EmployeeProvider.Provider1, "P1_001"),
        EmployeeSynchronizationAttempt.Create(
            EmployeeProfile.Create(
                "Alice",
                "Johnson",
                "alice.johnson@provider1.com",
                null,
                null,
                null,
                null,
                EmployeeStatus.Active),
            new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero)));

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string provider, string json) =>
        client.PostAsync(
            new Uri($"/api/v1/providers/{provider}/employees", UriKind.Relative),
            new StringContent(json, Encoding.UTF8, "application/json"));
}
