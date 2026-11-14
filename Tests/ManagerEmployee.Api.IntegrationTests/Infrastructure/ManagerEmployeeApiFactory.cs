using ManagerEmployee.Application.Synchronization;
using ManagerEmployee.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ManagerEmployee.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real API pipeline with an isolated in-memory database and a recording TrackTik fake.
/// Create one factory per test so tests stay independent under method-level parallelism.
/// </summary>
internal sealed class ManagerEmployeeApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"api-tests-{Guid.NewGuid():N}";

    public FakeTrackTikEmployeeClient TrackTik { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting(
            "ConnectionStrings:ManagerEmployeeDatabase",
            "Server=unused;Database=unused;TrustServerCertificate=True");
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
        builder.UseSetting("TrackTik:ClientId", "test-client");
        builder.UseSetting("TrackTik:ClientSecret", "test-secret");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ManagerEmployeeDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ManagerEmployeeDbContext>>();
            services.AddDbContext<ManagerEmployeeDbContext>(
                options => options
                    .UseInMemoryDatabase(_databaseName)
                    .AddInterceptors(new RowVersionInterceptor()));

            services.RemoveAll<ITrackTikEmployeeClient>();
            services.AddSingleton<ITrackTikEmployeeClient>(TrackTik);
        });
    }
}
