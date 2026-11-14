using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace ManagerEmployee.Infrastructure.Tests.TrackTik;

/// <summary>
/// Returns queued responses for the token endpoint and the employees API, and records every request.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly ConcurrentQueue<Func<HttpResponseMessage>> _tokenResponses = new();
    private readonly ConcurrentQueue<Func<HttpResponseMessage>> _apiResponses = new();
    private readonly ConcurrentQueue<RecordedRequest> _requests = new();

    public IEnumerable<RecordedRequest> TokenRequests => _requests.Where(request => request.IsTokenRequest);

    public IEnumerable<RecordedRequest> ApiRequests => _requests.Where(request => !request.IsTokenRequest);

    public void EnqueueToken(string accessToken, int expiresIn = 3600) =>
        _tokenResponses.Enqueue(() => Json(
            HttpStatusCode.OK,
            $$"""{"access_token":"{{accessToken}}","token_type":"Bearer","expires_in":{{expiresIn}}}"""));

    public void EnqueueToken(Func<HttpResponseMessage> response) => _tokenResponses.Enqueue(response);

    public void EnqueueApi(HttpStatusCode status, string body = "{}") =>
        _apiResponses.Enqueue(() => Json(status, body));

    public void EnqueueApi(Func<HttpResponseMessage> response) => _apiResponses.Enqueue(response);

    public static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var isTokenRequest = request.RequestUri!.AbsolutePath.EndsWith("/oauth/token", StringComparison.Ordinal);
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        _requests.Enqueue(new RecordedRequest(
            request.Method,
            request.RequestUri,
            request.Headers.Authorization?.ToString(),
            request.Content?.Headers.ContentType?.MediaType,
            body,
            isTokenRequest));

        var queue = isTokenRequest ? _tokenResponses : _apiResponses;
        return queue.TryDequeue(out var next)
            ? next()
            : throw new InvalidOperationException($"No stubbed response for {request.Method} {request.RequestUri}.");
    }

    internal sealed record RecordedRequest(
        HttpMethod Method,
        Uri Uri,
        string? Authorization,
        string? ContentType,
        string? Body,
        bool IsTokenRequest);
}
