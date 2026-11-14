namespace ManagerEmployee.Infrastructure.TrackTik;

/// <summary>
/// TrackTik connection settings bound from the <c>TrackTik</c> configuration section.
/// The URLs default to the assessment mock in appsettings.json; the credentials must come from
/// user secrets or environment variables and are never committed.
/// </summary>
public sealed class TrackTikOptions
{
    public const string SectionName = "TrackTik";

    /// <summary>The <c>/v1</c> API base URL.</summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>The OAuth2 token URL. It is not under the <c>/v1</c> API base URL.</summary>
    public string TokenUrl { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    internal static bool HasAbsoluteHttpUrls(TrackTikOptions options) =>
        IsAbsoluteHttpUrl(options.ApiBaseUrl) && IsAbsoluteHttpUrl(options.TokenUrl);

    internal static bool HasCredentials(TrackTikOptions options) =>
        !string.IsNullOrWhiteSpace(options.ClientId) && !string.IsNullOrWhiteSpace(options.ClientSecret);

    private static bool IsAbsoluteHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
