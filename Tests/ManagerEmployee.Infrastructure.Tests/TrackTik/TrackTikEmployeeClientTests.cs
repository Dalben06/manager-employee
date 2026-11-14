using System.Net;
using System.Text.Json;
using ManagerEmployee.Application.Synchronization;
using ManagerEmployee.Domain.Employees.Enums;
using ManagerEmployee.Infrastructure.TrackTik;
using Microsoft.Extensions.Options;

namespace ManagerEmployee.Infrastructure.Tests.TrackTik;

[TestClass]
public sealed class TrackTikEmployeeClientTests
{
    private const string ClientSecret = "super-secret-value";

    private static readonly TrackTikEmployee Alice = new()
    {
        EmployeeId = "provider1-P1_001",
        FirstName = "Alice",
        LastName = "Johnson",
        Email = "alice.johnson@provider1.com",
        PhoneNumber = "+1-555-0101",
        Position = "Security Officer",
        Department = "Security Operations",
        StartDate = new DateOnly(2024, 1, 15),
        Status = EmployeeStatus.Active,
    };

    private readonly StubHttpMessageHandler _handler = new();
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 11, 12, 12, 0, 0, TimeSpan.Zero));

    [TestMethod]
    public async Task CreateAsync_RequestsFormEncodedTokenThenPostsEmployeeWithBearer()
    {
        _handler.EnqueueToken("token-1");
        _handler.EnqueueApi(HttpStatusCode.Created);

        var result = await CreateClient().CreateAsync(Alice, CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        var tokenRequest = _handler.TokenRequests.Single();
        Assert.AreEqual("https://tracktik.test/oauth/token", tokenRequest.Uri.ToString());
        Assert.AreEqual("application/x-www-form-urlencoded", tokenRequest.ContentType);
        StringAssert.Contains(tokenRequest.Body, "grant_type=client_credentials");
        StringAssert.Contains(tokenRequest.Body, "client_id=client-1");
        Assert.DoesNotContain("scope", tokenRequest.Body!);

        var apiRequest = _handler.ApiRequests.Single();
        Assert.AreEqual(HttpMethod.Post, apiRequest.Method);
        Assert.AreEqual("https://tracktik.test/v1/employees", apiRequest.Uri.ToString());
        Assert.AreEqual("Bearer token-1", apiRequest.Authorization);
        using var body = JsonDocument.Parse(apiRequest.Body!);
        Assert.AreEqual("provider1-P1_001", body.RootElement.GetProperty("employeeId").GetString());
        Assert.AreEqual("2024-01-15", body.RootElement.GetProperty("startDate").GetString());
        Assert.AreEqual("active", body.RootElement.GetProperty("status").GetString());
    }

    [TestMethod]
    public async Task UpdateAsync_PutsEmployeeWithoutIdentifierOrStartDate()
    {
        _handler.EnqueueToken("token-1");
        _handler.EnqueueApi(HttpStatusCode.OK);

        var result = await CreateClient().UpdateAsync(Alice, CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        var apiRequest = _handler.ApiRequests.Single();
        Assert.AreEqual(HttpMethod.Put, apiRequest.Method);
        Assert.AreEqual("https://tracktik.test/v1/employees/provider1-P1_001", apiRequest.Uri.ToString());
        using var body = JsonDocument.Parse(apiRequest.Body!);
        Assert.IsFalse(body.RootElement.TryGetProperty("employeeId", out _));
        Assert.IsFalse(body.RootElement.TryGetProperty("startDate", out _));
        Assert.AreEqual("Alice", body.RootElement.GetProperty("firstName").GetString());
    }

    [TestMethod]
    public async Task Token_IsReusedUntilShortlyBeforeExpiry_ThenRefreshed()
    {
        _handler.EnqueueToken("token-1", expiresIn: 3600);
        _handler.EnqueueToken("token-2", expiresIn: 3600);
        _handler.EnqueueApi(HttpStatusCode.Created);
        _handler.EnqueueApi(HttpStatusCode.OK);
        _handler.EnqueueApi(HttpStatusCode.OK);
        var client = CreateClient();

        await client.CreateAsync(Alice, CancellationToken.None);
        _time.Advance(TimeSpan.FromSeconds(3600 - 61));
        await client.UpdateAsync(Alice, CancellationToken.None);
        _time.Advance(TimeSpan.FromSeconds(2));
        await client.UpdateAsync(Alice, CancellationToken.None);

        Assert.AreEqual(2, _handler.TokenRequests.Count());
        CollectionAssert.AreEqual(
            new[] { "Bearer token-1", "Bearer token-1", "Bearer token-2" },
            _handler.ApiRequests.Select(request => request.Authorization).ToArray());
    }

    [TestMethod]
    public async Task ShortLivedToken_IsReusedForHalfItsLifetime()
    {
        _handler.EnqueueToken("token-1", expiresIn: 30);
        _handler.EnqueueToken("token-2", expiresIn: 30);
        _handler.EnqueueApi(HttpStatusCode.Created);
        _handler.EnqueueApi(HttpStatusCode.OK);
        _handler.EnqueueApi(HttpStatusCode.OK);
        var client = CreateClient();

        await client.CreateAsync(Alice, CancellationToken.None);
        _time.Advance(TimeSpan.FromSeconds(14));
        await client.UpdateAsync(Alice, CancellationToken.None);
        _time.Advance(TimeSpan.FromSeconds(2));
        await client.UpdateAsync(Alice, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "Bearer token-1", "Bearer token-1", "Bearer token-2" },
            _handler.ApiRequests.Select(request => request.Authorization).ToArray());
    }

    [TestMethod]
    public async Task MalformedTokenResponse_FailsWithInvalidTokenMessage()
    {
        _handler.EnqueueToken(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>gateway</html>", System.Text.Encoding.UTF8, "text/html"),
        });

        var result = await CreateClient().CreateAsync(Alice, CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual("TrackTik token response was invalid.", result.Error);
        Assert.IsEmpty(_handler.ApiRequests);
    }

    [TestMethod]
    public async Task Unauthorized_RefreshesTokenAndRetriesOnce()
    {
        _handler.EnqueueToken("stale");
        _handler.EnqueueToken("fresh");
        _handler.EnqueueApi(HttpStatusCode.Unauthorized);
        _handler.EnqueueApi(HttpStatusCode.Created);

        var result = await CreateClient().CreateAsync(Alice, CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.AreEqual(
            new[] { "Bearer stale", "Bearer fresh" },
            _handler.ApiRequests.Select(request => request.Authorization).ToArray());
    }

    [TestMethod]
    public async Task Unauthorized_AfterRetry_FailsWithoutLooping()
    {
        _handler.EnqueueToken("token-1");
        _handler.EnqueueToken("token-2");
        _handler.EnqueueApi(HttpStatusCode.Unauthorized);
        _handler.EnqueueApi(HttpStatusCode.Unauthorized);

        var result = await CreateClient().CreateAsync(Alice, CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual("TrackTik returned 401.", result.Error);
        Assert.AreEqual(2, _handler.ApiRequests.Count());
    }

    [TestMethod]
    public async Task ErrorEnvelope_IsTranslatedIntoSafeLastError()
    {
        _handler.EnqueueToken("token-1");
        _handler.EnqueueApi(
            HttpStatusCode.BadRequest,
            """{"success":false,"error":{"code":"VALIDATION_ERROR","message":"Invalid employee data","details":[{"field":"email","message":"Invalid email format"}]}}""");

        var result = await CreateClient().CreateAsync(Alice, CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(
            "TrackTik returned 400 VALIDATION_ERROR: Invalid employee data (email: Invalid email format)",
            result.Error);
    }

    [TestMethod]
    [DataRow(HttpStatusCode.TooManyRequests)]
    [DataRow(HttpStatusCode.InternalServerError)]
    [DataRow(HttpStatusCode.ServiceUnavailable)]
    public async Task NonSuccessStatusWithoutEnvelope_Fails(HttpStatusCode status)
    {
        _handler.EnqueueToken("token-1");
        _handler.EnqueueApi(() => new HttpResponseMessage(status));

        var result = await CreateClient().CreateAsync(Alice, CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual($"TrackTik returned {(int)status}.", result.Error);
    }

    [TestMethod]
    public async Task Timeout_FailsWithTimeoutMessage()
    {
        _handler.EnqueueToken("token-1");
        _handler.EnqueueApi(() => throw new TaskCanceledException("timeout", new TimeoutException()));

        var result = await CreateClient().CreateAsync(Alice, CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual("TrackTik request timed out.", result.Error);
    }

    [TestMethod]
    public async Task NetworkFailure_FailsWithUnreachableMessage()
    {
        _handler.EnqueueToken("token-1");
        _handler.EnqueueApi(() => throw new HttpRequestException("connection refused"));

        var result = await CreateClient().CreateAsync(Alice, CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual("TrackTik could not be reached.", result.Error);
    }

    [TestMethod]
    public async Task TokenRejected_FailsWithoutLeakingCredentials()
    {
        _handler.EnqueueToken(() => StubHttpMessageHandler.Json(
            HttpStatusCode.Unauthorized,
            """{"error":"invalid_client"}"""));

        var result = await CreateClient().CreateAsync(Alice, CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual("TrackTik token request failed: 401 invalid_client", result.Error);
        Assert.DoesNotContain(ClientSecret, result.Error!);
        Assert.IsEmpty(_handler.ApiRequests);
    }

    [TestMethod]
    public async Task CallerCancellation_IsNotReportedAsFailure()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => CreateClient().CreateAsync(Alice, cancellation.Token));
    }

    private TrackTikEmployeeClient CreateClient()
    {
        var options = Options.Create(new TrackTikOptions
        {
            ApiBaseUrl = "https://tracktik.test/v1",
            TokenUrl = "https://tracktik.test/oauth/token",
            ClientId = "client-1",
            ClientSecret = ClientSecret,
        });
        var tokenProvider = new TrackTikTokenProvider(new StubHttpClientFactory(_handler), options, _time);
        var httpClient = new HttpClient(_handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://tracktik.test/v1/"),
        };
        return new TrackTikEmployeeClient(httpClient, tokenProvider);
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
