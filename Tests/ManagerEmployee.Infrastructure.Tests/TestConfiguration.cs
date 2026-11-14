using Microsoft.Extensions.Configuration;

namespace ManagerEmployee.Infrastructure.Tests;

/// <summary>
/// Builds an in-memory configuration with every setting <c>AddInfrastructure</c> requires.
/// </summary>
internal static class TestConfiguration
{
    public const string ClientSecret = "test-client-secret";

    public static IConfiguration Create(Dictionary<string, string?>? overrides = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:ManagerEmployeeDatabase"] =
                "Server=localhost;Database=RegistrationInspection;Integrated Security=True;TrustServerCertificate=True",
            ["TrackTik:ApiBaseUrl"] = "https://tracktik.test/v1",
            ["TrackTik:TokenUrl"] = "https://tracktik.test/oauth/token",
            ["TrackTik:ClientId"] = "test-client",
            ["TrackTik:ClientSecret"] = ClientSecret,
        };

        foreach (var (key, value) in overrides ?? [])
        {
            settings[key] = value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }
}
