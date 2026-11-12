namespace ManagerEmployee.Application.Synchronization;

public sealed record TrackTikSyncResult(bool IsSuccess, string? Error)
{
    public static TrackTikSyncResult Success() => new(true, null);

    public static TrackTikSyncResult Failure(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new TrackTikSyncResult(false, error);
    }
}
