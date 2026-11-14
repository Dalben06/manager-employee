using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ManagerEmployee.Api.IntegrationTests.Infrastructure;

namespace ManagerEmployee.Api.IntegrationTests;

/// <summary>
/// Verifies the API's versioned OpenAPI document.
/// </summary>
[TestClass]
public sealed class OpenApiTests
{
    [TestMethod]
    public async Task OpenApiDocument_DescribesVersionedEmployeeRoutes()
    {
        using var factory = new ManagerEmployeeApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("application/json", response.Content.Headers.ContentType?.MediaType);
        var paths = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("paths");
        Assert.IsTrue(paths.TryGetProperty("/api/v1/providers/{provider}/employees", out var submit));
        Assert.IsTrue(submit.TryGetProperty("post", out _));
        Assert.IsTrue(paths.TryGetProperty("/api/v1/employees/{provider}/{providerEmployeeId}", out var get));
        Assert.IsTrue(get.TryGetProperty("get", out _));
    }

    [TestMethod]
    public async Task UnsupportedApiVersion_ReturnsProblemDetails()
    {
        using var factory = new ManagerEmployeeApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v2/employees/provider1/P1_001", UriKind.Relative));

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.AreEqual("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
