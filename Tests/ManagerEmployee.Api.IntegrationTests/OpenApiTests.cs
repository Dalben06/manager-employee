using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ManagerEmployee.Api.IntegrationTests;

/// <summary>
/// Verifies the API's OpenAPI boundary.
/// </summary>
[TestClass]
public sealed class OpenApiTests
{
    [TestMethod]
    public async Task OpenApiDocument_IsAvailableInDevelopment()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
        });

        using var response = await client.GetAsync("/openapi/v1.json");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("application/json", response.Content.Headers.ContentType?.MediaType);
    }
}
