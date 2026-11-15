using ManagerEmployee.Api.IntegrationTests.Infrastructure;
using ManagerEmployee.Application.Synchronization;
using ManagerEmployee.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ManagerEmployee.Api.ContainerTests.Infrastructure;

/// <summary>
/// Hosts the real API pipeline against a fresh SQL Server database. Migrations run at startup through the
/// same <c>Database:ApplyMigrationsOnStartup</c> path Compose uses; only TrackTik is replaced by a fake.
/// </summary>
internal sealed class SqlServerApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString = SqlServerContainer.CreateDatabaseConnectionString();

    public FakeTrackTikEmployeeClient TrackTik { get; } = new();

    /// <summary>Creates a scope whose <see cref="ManagerEmployeeDbContext"/> targets this test's database.</summary>
    public AsyncServiceScope CreateScope() => Services.CreateAsyncScope();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:ManagerEmployeeDatabase", _connectionString);
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("TrackTik:ClientId", "test-client");
        builder.UseSetting("TrackTik:ClientSecret", "test-secret");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ITrackTikEmployeeClient>();
            services.AddSingleton<ITrackTikEmployeeClient>(TrackTik);
        });
    }
}
