using System.Collections.Concurrent;
using ManagerEmployee.Application.Synchronization;

namespace ManagerEmployee.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Records TrackTik calls and returns a configurable result, standing in for the external API.
/// </summary>
internal sealed class FakeTrackTikEmployeeClient : ITrackTikEmployeeClient
{
    private readonly ConcurrentQueue<string> _calls = new();

    public TrackTikSyncResult Result { get; set; } = TrackTikSyncResult.Success();

    public IReadOnlyList<string> Calls => [.. _calls];

    public Task<TrackTikSyncResult> CreateAsync(
        TrackTikEmployee employee,
        CancellationToken cancellationToken)
    {
        _calls.Enqueue($"create:{employee.EmployeeId}");
        return Task.FromResult(Result);
    }

    public Task<TrackTikSyncResult> UpdateAsync(
        TrackTikEmployee employee,
        CancellationToken cancellationToken)
    {
        _calls.Enqueue($"update:{employee.EmployeeId}");
        return Task.FromResult(Result);
    }
}
