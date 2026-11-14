namespace ManagerEmployee.Infrastructure.TrackTik;

/// <summary>
/// Raised when a TrackTik access token cannot be obtained. The message carries only the HTTP status
/// and OAuth error code, never credentials.
/// </summary>
public sealed class TrackTikAuthenticationException : Exception
{
    public TrackTikAuthenticationException()
    {
    }

    public TrackTikAuthenticationException(string message)
        : base(message)
    {
    }

    public TrackTikAuthenticationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
