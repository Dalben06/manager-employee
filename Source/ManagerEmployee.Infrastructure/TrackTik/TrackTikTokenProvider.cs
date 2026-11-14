using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace ManagerEmployee.Infrastructure.TrackTik;

/// <summary>
/// Obtains OAuth2 client-credentials tokens and caches them until shortly before they expire.
/// Registered as a singleton so every request shares the cached token.
/// </summary>
internal sealed class TrackTikTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<TrackTikOptions> options,
    TimeProvider timeProvider) : IDisposable
{
    public const string HttpClientName = "TrackTik.Token";

    private static readonly TimeSpan MaximumExpirySafetyWindow = TimeSpan.FromSeconds(60);

    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private CachedToken? _cached;

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (TryGetCachedToken(out var cached))
        {
            return cached;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (TryGetCachedToken(out cached))
            {
                return cached;
            }

            var token = await RequestTokenAsync(cancellationToken);
            // Refresh 60s before expiry, or halfway through the lifetime of short-lived tokens, so a
            // token with expires_in <= 60 is still reused instead of being requested for every call.
            var lifetime = TimeSpan.FromSeconds(token.ExpiresIn);
            var safetyWindow = TimeSpan.FromTicks(
                Math.Min(MaximumExpirySafetyWindow.Ticks, lifetime.Ticks / 2));
            var refreshAt = timeProvider.GetUtcNow().Add(lifetime - safetyWindow);
            Volatile.Write(ref _cached, new CachedToken(token.AccessToken, refreshAt));
            return token.AccessToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>Drops the cached token, for example after TrackTik rejects it with 401.</summary>
    public void Invalidate(string rejectedToken)
    {
        // Only clear the token that was rejected, so a concurrent refresh is not discarded.
        var current = Volatile.Read(ref _cached);
        if (current?.Value == rejectedToken)
        {
            Interlocked.CompareExchange(ref _cached, null, current);
        }
    }

    public void Dispose() => _refreshLock.Dispose();

    private bool TryGetCachedToken(out string token)
    {
        var cached = Volatile.Read(ref _cached);
        token = cached?.Value ?? string.Empty;
        return cached is not null && timeProvider.GetUtcNow() < cached.RefreshAt;
    }

    private async Task<TokenResponse> RequestTokenAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = settings.ClientId,
            ["client_secret"] = settings.ClientSecret,
        });

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.PostAsync(new Uri(settings.TokenUrl), content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await ReadOAuthErrorAsync(response, cancellationToken);
            throw new TrackTikAuthenticationException(
                $"TrackTik token request failed: {(int)response.StatusCode} {error}".TrimEnd());
        }

        TokenResponse? token;
        try
        {
            token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
        }
        catch (System.Text.Json.JsonException)
        {
            token = null;
        }

        if (string.IsNullOrWhiteSpace(token?.AccessToken) || token.ExpiresIn <= 0)
        {
            throw new TrackTikAuthenticationException("TrackTik token response was invalid.");
        }

        return token;
    }

    private static async Task<string> ReadOAuthErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<OAuthErrorResponse>(cancellationToken);
            return body?.Error ?? string.Empty;
        }
        catch (System.Text.Json.JsonException)
        {
            return string.Empty;
        }
    }

    private sealed record CachedToken(string Value, DateTimeOffset RefreshAt);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private sealed record OAuthErrorResponse(
        [property: JsonPropertyName("error")] string? Error);
}
