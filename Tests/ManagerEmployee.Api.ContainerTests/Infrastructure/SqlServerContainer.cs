using DotNet.Testcontainers.Builders;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace ManagerEmployee.Api.ContainerTests.Infrastructure;

/// <summary>
/// Starts one SQL Server container for the whole test assembly. Each test gets its own database in it,
/// so tests stay independent under method-level parallelism. When Docker is not reachable, every test
/// reports Inconclusive instead of failing, so the solution-wide test command still passes without Docker.
/// </summary>
[TestClass]
public static class SqlServerContainer
{
    // Same image as docker-compose.yml.
    private const string Image = "mcr.microsoft.com/mssql/server:2022-latest";

    private static MsSqlContainer? _container;
    private static string? _unavailableReason;

    [AssemblyInitialize]
    public static async Task StartAsync(TestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            _container = new MsSqlBuilder(Image).Build();
            await _container.StartAsync(context.CancellationToken);
        }
        catch (DockerUnavailableException exception)
        {
            _container = null;
            _unavailableReason = exception.Message;
        }
    }

    [AssemblyCleanup]
    public static async Task StopAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>
    /// Returns a connection string for a new, uniquely named database, or marks the test Inconclusive without Docker.
    /// </summary>
    public static string CreateDatabaseConnectionString()
    {
        if (_container is null)
        {
            Assert.Inconclusive($"Docker is not available, so SQL Server container tests were skipped. {_unavailableReason}");
        }

        return new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = $"ManagerEmployee_{Guid.NewGuid():N}",
            TrustServerCertificate = true,
        }.ConnectionString;
    }
}
