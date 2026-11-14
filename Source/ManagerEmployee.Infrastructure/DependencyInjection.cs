using ManagerEmployee.Application.Employees.Ports;
using ManagerEmployee.Application.Events;
using ManagerEmployee.Application.Providers;
using ManagerEmployee.Application.Synchronization;
using ManagerEmployee.Domain.Employees.Repositories;
using ManagerEmployee.Infrastructure.Events;
using ManagerEmployee.Infrastructure.Persistence;
using ManagerEmployee.Infrastructure.Persistence.ReadStores;
using ManagerEmployee.Infrastructure.Persistence.Repositories;
using ManagerEmployee.Infrastructure.Providers.Provider1;
using ManagerEmployee.Infrastructure.Providers.Provider2;
using ManagerEmployee.Infrastructure.TrackTik;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ManagerEmployee.Infrastructure;

public static class DependencyInjection
{
    private const string ConnectionStringName = "ManagerEmployeeDatabase";
    private static readonly TimeSpan TrackTikTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Registers persistence, provider adapters, event publication, and the TrackTik client.
    /// Requires the <c>ManagerEmployeeDatabase</c> connection string and a valid <c>TrackTik</c> section;
    /// TrackTik settings are validated when the host starts.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is required.");
        }

        services.AddDbContext<ManagerEmployeeDbContext>(
            options => options.UseSqlServer(connectionString));
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IEmployeeReadStore, EmployeeReadStore>();
        services.AddScoped<IEmployeeSyncedEventPublisher, InMemoryEmployeeSyncedEventPublisher>();
        services.AddSingleton<IProviderEmployeeMapper, Provider1EmployeeMapper>();
        services.AddSingleton<IProviderEmployeeMapper, Provider2EmployeeMapper>();
        services.AddTrackTik(configuration);

        return services;
    }

    private static void AddTrackTik(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TrackTikOptions>()
            .Bind(configuration.GetSection(TrackTikOptions.SectionName))
            .Validate(
                TrackTikOptions.HasAbsoluteHttpUrls,
                "TrackTik:ApiBaseUrl and TrackTik:TokenUrl must be absolute http(s) URLs.")
            .Validate(
                TrackTikOptions.HasCredentials,
                "TrackTik:ClientId and TrackTik:ClientSecret are required. Set them with user secrets or environment variables.")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient(TrackTikTokenProvider.HttpClientName, client => client.Timeout = TrackTikTimeout);
        services.AddSingleton<TrackTikTokenProvider>();
        services.AddHttpClient<ITrackTikEmployeeClient, TrackTikEmployeeClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<TrackTikOptions>>().Value;
            client.BaseAddress = new Uri(options.ApiBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TrackTikTimeout;
        });
    }
}
