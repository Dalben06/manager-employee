namespace ManagerEmployee.Application.Synchronization;

public interface ITrackTikEmployeeClient
{
    Task<TrackTikSyncResult> CreateAsync(
        TrackTikEmployee employee,
        CancellationToken cancellationToken);

    Task<TrackTikSyncResult> UpdateAsync(
        TrackTikEmployee employee,
        CancellationToken cancellationToken);
}
