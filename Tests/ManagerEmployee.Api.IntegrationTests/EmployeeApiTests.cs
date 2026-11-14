using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ManagerEmployee.Api.IntegrationTests.Infrastructure;
using ManagerEmployee.Application.Employees.Models;
using ManagerEmployee.Application.Employees.Ports;
using ManagerEmployee.Application.Synchronization;
using ManagerEmployee.Domain.Employees.Enums;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ManagerEmployee.Api.IntegrationTests;

/// <summary>
/// Exercises the versioned employee routes end to end through the real HTTP pipeline.
/// </summary>
[TestClass]
public sealed class EmployeeApiTests
{
    private const string ProblemJson = "application/problem+json";

    [TestMethod]
    public async Task Submit_NewEmployee_Returns201WithLocationAndCreatesInTrackTik()
    {
        using var factory = new ManagerEmployeeApiFactory();
        using var client = factory.CreateClient();

        using var response = await PostAsync(client, "provider1", ProviderPayloads.Provider1Alice);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.AreEqual("/api/v1/employees/provider1/P1_001", response.Headers.Location?.AbsolutePath);
        var body = await ReadJsonAsync(response);
        Assert.AreEqual("provider1-P1_001", body.GetProperty("employeeId").GetString());
        Assert.AreEqual("synced", body.GetProperty("syncStatus").GetString());
        Assert.AreEqual(1, body.GetProperty("attempts").GetInt32());
        CollectionAssert.AreEqual(new[] { "create:provider1-P1_001" }, factory.TrackTik.Calls.ToArray());
    }

    [TestMethod]
    public async Task Submit_PreviouslySyncedEmployee_Returns200AndUpdatesInTrackTik()
    {
        using var factory = new ManagerEmployeeApiFactory();
        using var client = factory.CreateClient();
        using var first = await PostAsync(client, "provider1", ProviderPayloads.Provider1Alice);

        using var response = await PostAsync(client, "provider1", ProviderPayloads.Provider1Alice);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.AreEqual(2, body.GetProperty("attempts").GetInt32());
        Assert.AreEqual("synced", body.GetProperty("syncStatus").GetString());
        CollectionAssert.AreEqual(
            new[] { "create:provider1-P1_001", "update:provider1-P1_001" },
            factory.TrackTik.Calls.ToArray());
    }

    [TestMethod]
    public async Task Get_AfterProvider2Submission_ReturnsStoredEmployeeAndSyncState()
    {
        using var factory = new ManagerEmployeeApiFactory();
        using var client = factory.CreateClient();
        using var submitted = await PostAsync(client, "provider2", ProviderPayloads.Provider2Carol);

        using var response = await client.GetAsync(new Uri("/api/v1/employees/provider2/P2_001", UriKind.Relative));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.AreEqual("provider2", body.GetProperty("provider").GetString());
        Assert.AreEqual("P2_001", body.GetProperty("providerEmployeeId").GetString());
        Assert.AreEqual("provider2-P2_001", body.GetProperty("employeeId").GetString());
        Assert.AreEqual("Carol", body.GetProperty("firstName").GetString());
        Assert.AreEqual("Davis", body.GetProperty("lastName").GetString());
        Assert.AreEqual("carol.davis@provider2.com", body.GetProperty("email").GetString());
        Assert.AreEqual("+1-555-0201", body.GetProperty("phoneNumber").GetString());
        Assert.AreEqual("Security Guard", body.GetProperty("position").GetString());
        Assert.AreEqual("Night Shift Security", body.GetProperty("department").GetString());
        Assert.AreEqual("2024-02-01", body.GetProperty("startDate").GetString());
        Assert.AreEqual("active", body.GetProperty("status").GetString());
        Assert.AreEqual("synced", body.GetProperty("syncStatus").GetString());
        Assert.AreEqual(1, body.GetProperty("attempts").GetInt32());
        Assert.AreEqual(JsonValueKind.Null, body.GetProperty("lastError").ValueKind);
        Assert.AreEqual(JsonValueKind.String, body.GetProperty("lastAttemptAt").ValueKind);
    }

    [TestMethod]
    public async Task Submit_InvalidPayload_Returns400ValidationProblemAndDoesNotCallTrackTik()
    {
        using var factory = new ManagerEmployeeApiFactory();
        using var client = factory.CreateClient();

        using var response = await PostAsync(client, "provider1", ProviderPayloads.Provider1InvalidEmail);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.AreEqual(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        var body = await ReadJsonAsync(response);
        Assert.IsTrue(body.GetProperty("errors").TryGetProperty("email_address", out _));
        Assert.IsEmpty(factory.TrackTik.Calls);

        using var lookup = await client.GetAsync(new Uri("/api/v1/employees/provider1/P1_999", UriKind.Relative));
        Assert.AreEqual(HttpStatusCode.NotFound, lookup.StatusCode);
    }

    [TestMethod]
    [DataRow("POST", "/api/v1/providers/provider3/employees")]
    [DataRow("GET", "/api/v1/employees/provider3/P1_001")]
    public async Task UnknownProvider_Returns404ProblemDetails(string method, string path)
    {
        using var factory = new ManagerEmployeeApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST")
        {
            request.Content = JsonContent(ProviderPayloads.Provider1Alice);
        }

        using var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.AreEqual(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        var body = await ReadJsonAsync(response);
        Assert.AreEqual("Unknown provider.", body.GetProperty("title").GetString());
        Assert.IsEmpty(factory.TrackTik.Calls);
    }

    [TestMethod]
    public async Task Get_MissingEmployee_Returns404ProblemDetails()
    {
        using var factory = new ManagerEmployeeApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v1/employees/provider1/UNKNOWN", UriKind.Relative));

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.AreEqual(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        var body = await ReadJsonAsync(response);
        Assert.AreEqual("Employee not found.", body.GetProperty("title").GetString());
    }

    [TestMethod]
    public async Task Submit_TrackTikFailure_Returns502WithSyncStatusAndPersistsFailedState()
    {
        using var factory = new ManagerEmployeeApiFactory();
        factory.TrackTik.Result = TrackTikSyncResult.Failure("TrackTik returned 503 ServiceUnavailable.");
        using var client = factory.CreateClient();

        using var response = await PostAsync(client, "provider1", ProviderPayloads.Provider1Alice);

        Assert.AreEqual(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.AreEqual(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadJsonAsync(response);
        Assert.AreEqual("failed", problem.GetProperty("syncStatus").GetString());
        Assert.AreEqual("provider1-P1_001", problem.GetProperty("employeeId").GetString());
        Assert.AreEqual(1, problem.GetProperty("attempts").GetInt32());

        using var lookup = await client.GetAsync(new Uri("/api/v1/employees/provider1/P1_001", UriKind.Relative));
        var stored = await ReadJsonAsync(lookup);
        Assert.AreEqual("failed", stored.GetProperty("syncStatus").GetString());
        Assert.AreEqual("TrackTik returned 503 ServiceUnavailable.", stored.GetProperty("lastError").GetString());
    }

    [TestMethod]
    public async Task UnhandledException_Returns500ProblemDetailsWithoutDetails()
    {
        using var factory = new ManagerEmployeeApiFactory();
        using var throwingFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddScoped<IEmployeeReadStore, ThrowingEmployeeReadStore>()));
        using var client = throwingFactory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v1/employees/provider1/P1_001", UriKind.Relative));

        Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.AreEqual(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(ThrowingEmployeeReadStore.Secret, raw);
        using var body = JsonDocument.Parse(raw);
        Assert.IsTrue(body.RootElement.TryGetProperty("traceId", out _));
    }

    [TestMethod]
    public async Task Submit_NonJsonContentType_Returns415ProblemDetails()
    {
        using var factory = new ManagerEmployeeApiFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent(ProviderPayloads.Provider1Alice, Encoding.UTF8, "text/plain");

        using var response = await client.PostAsync(
            new Uri("/api/v1/providers/provider1/employees", UriKind.Relative),
            content);

        Assert.AreEqual(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.AreEqual(ProblemJson, response.Content.Headers.ContentType?.MediaType);
    }

    private sealed class ThrowingEmployeeReadStore : IEmployeeReadStore
    {
        public const string Secret = "connection-string-with-a-secret";

        public Task<EmployeeDetails?> FindAsync(
            EmployeeProvider provider,
            string providerEmployeeId,
            CancellationToken cancellationToken) => throw new InvalidOperationException(Secret);
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string provider, string json) =>
        client.PostAsync(
            new Uri($"/api/v1/providers/{provider}/employees", UriKind.Relative),
            JsonContent(json));

    private static StringContent JsonContent(string json) =>
        new(json, Encoding.UTF8, "application/json");

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();
}
