using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ManagerEmployee.Api.IntegrationTests.Infrastructure;
using ManagerEmployee.Application.Employees.Ports;
using ManagerEmployee.Domain.Employees.Enums;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ManagerEmployee.Api.IntegrationTests;

/// <summary>
/// Verifies the report route's transport contract. The EF InMemory provider cannot execute the stored
/// procedure, so the reader is faked here; the real procedure is exercised by the container tests.
/// </summary>
[TestClass]
public sealed class ReportApiTests
{
    [TestMethod]
    public async Task GetSyncStatusReport_ReturnsEveryStatusAndTotal()
    {
        using var factory = new ManagerEmployeeApiFactory();
        using var reportFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddScoped<IEmployeeSyncStatusReportReader, FakeReportReader>()));
        using var client = reportFactory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v1/reports/sync-status", UriKind.Relative));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual(0, body.GetProperty("pending").GetInt32());
        Assert.AreEqual(3, body.GetProperty("synced").GetInt32());
        Assert.AreEqual(2, body.GetProperty("failed").GetInt32());
        Assert.AreEqual(5, body.GetProperty("total").GetInt32());
    }

    private sealed class FakeReportReader : IEmployeeSyncStatusReportReader
    {
        public Task<IReadOnlyDictionary<SynchronizationStatus, int>> CountBySyncStatusAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<SynchronizationStatus, int>>(
                new Dictionary<SynchronizationStatus, int>
                {
                    [SynchronizationStatus.Synced] = 3,
                    [SynchronizationStatus.Failed] = 2,
                });
    }
}
