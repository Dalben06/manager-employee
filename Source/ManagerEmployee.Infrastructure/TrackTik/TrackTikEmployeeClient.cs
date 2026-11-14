using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManagerEmployee.Application.Synchronization;
using ManagerEmployee.Domain.Employees.Enums;

namespace ManagerEmployee.Infrastructure.TrackTik;

/// <summary>
/// Calls the TrackTik employees API with a cached OAuth2 bearer token.
/// Remote failures are returned as <see cref="TrackTikSyncResult.Failure"/> with a message that is safe
/// to persist as <c>lastError</c>; credentials and tokens never appear in it.
/// </summary>
internal sealed class TrackTikEmployeeClient(
    HttpClient httpClient,
    TrackTikTokenProvider tokenProvider) : ITrackTikEmployeeClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public Task<TrackTikSyncResult> CreateAsync(
        TrackTikEmployee employee,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(employee);

        var body = new CreateEmployeeRequest(
            employee.EmployeeId,
            employee.FirstName,
            employee.LastName,
            employee.Email,
            employee.PhoneNumber,
            employee.Position,
            employee.Department,
            employee.StartDate,
            employee.Status.ToContractValue());

        return SendAsync(HttpMethod.Post, "employees", body, HttpStatusCode.Created, cancellationToken);
    }

    public Task<TrackTikSyncResult> UpdateAsync(
        TrackTikEmployee employee,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(employee);

        // TrackTik's update body omits employeeId (it is in the path) and startDate.
        var body = new UpdateEmployeeRequest(
            employee.FirstName,
            employee.LastName,
            employee.Email,
            employee.PhoneNumber,
            employee.Position,
            employee.Department,
            employee.Status.ToContractValue());

        return SendAsync(
            HttpMethod.Put,
            $"employees/{Uri.EscapeDataString(employee.EmployeeId)}",
            body,
            HttpStatusCode.OK,
            cancellationToken);
    }

    private async Task<TrackTikSyncResult> SendAsync<TBody>(
        HttpMethod method,
        string path,
        TBody body,
        HttpStatusCode expectedStatus,
        CancellationToken cancellationToken)
    {
        try
        {
            var token = await tokenProvider.GetTokenAsync(cancellationToken);
            using var response = await SendOnceAsync(method, path, body, token, cancellationToken);

            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                return await ToResultAsync(response, expectedStatus, cancellationToken);
            }

            // The token was rejected (revoked or expired early): refresh it and retry exactly once.
            tokenProvider.Invalidate(token);
            var freshToken = await tokenProvider.GetTokenAsync(cancellationToken);
            using var retried = await SendOnceAsync(method, path, body, freshToken, cancellationToken);
            return await ToResultAsync(retried, expectedStatus, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            return TrackTikSyncResult.Failure("TrackTik request timed out.");
        }
        catch (HttpRequestException)
        {
            return TrackTikSyncResult.Failure("TrackTik could not be reached.");
        }
        catch (TrackTikAuthenticationException exception)
        {
            return TrackTikSyncResult.Failure(exception.Message);
        }
    }

    private async Task<HttpResponseMessage> SendOnceAsync<TBody>(
        HttpMethod method,
        string path,
        TBody body,
        string token,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative))
        {
            Content = JsonContent.Create(body, options: Json),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await httpClient.SendAsync(request, cancellationToken);
    }

    private static async Task<TrackTikSyncResult> ToResultAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode == expectedStatus)
        {
            return TrackTikSyncResult.Success();
        }

        var status = (int)response.StatusCode;
        var error = await ReadErrorAsync(response, cancellationToken);
        return TrackTikSyncResult.Failure(error is null
            ? $"TrackTik returned {status}."
            : $"TrackTik returned {status} {error.Code}: {error.Message}{FormatDetails(error.Details)}");
    }

    private static async Task<ErrorBody?> ReadErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var envelope = await response.Content.ReadFromJsonAsync<ErrorEnvelope>(Json, cancellationToken);
            return envelope?.Error is { Code: not null } error ? error : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string FormatDetails(IReadOnlyList<ErrorDetail>? details) =>
        details is null || details.Count == 0
            ? string.Empty
            : " (" + string.Join("; ", details.Select(detail => $"{detail.Field}: {detail.Message}")) + ")";

    private sealed record CreateEmployeeRequest(
        string EmployeeId,
        string FirstName,
        string LastName,
        string Email,
        string? PhoneNumber,
        string? Position,
        string? Department,
        DateOnly? StartDate,
        string Status);

    private sealed record UpdateEmployeeRequest(
        string FirstName,
        string LastName,
        string Email,
        string? PhoneNumber,
        string? Position,
        string? Department,
        string Status);

    private sealed record ErrorEnvelope(ErrorBody? Error);

    private sealed record ErrorBody(string? Code, string? Message, IReadOnlyList<ErrorDetail>? Details);

    private sealed record ErrorDetail(string? Field, string? Message);
}
