using System.Text.Json;
using Asp.Versioning;
using ManagerEmployee.Api.Contracts;
using ManagerEmployee.Api.Errors;
using ManagerEmployee.Application.Employees.Commands.SyncEmployee;
using ManagerEmployee.Application.Employees.Models;
using Microsoft.AspNetCore.Mvc;

namespace ManagerEmployee.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/providers/{provider}/employees")]
public sealed class ProviderEmployeesController(SyncEmployeeCommandHandler syncEmployee) : ControllerBase
{
    private const int MaximumPayloadBytes = 64 * 1024;

    /// <summary>
    /// Validates and stores a provider employee, then synchronizes it to TrackTik.
    /// Returns 201 when the employee is stored for the first time and 200 for a resubmission.
    /// </summary>
    /// <param name="provider">The provider route value: <c>provider1</c> or <c>provider2</c>.</param>
    /// <param name="payload">One employee in the provider's JSON schema.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    [HttpPost]
    [Consumes("application/json")]
    [RequestSizeLimit(MaximumPayloadBytes)]
    [ProducesResponseType<EmployeeResponse>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<EmployeeResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway, "application/problem+json")]
    public async Task<IActionResult> Submit(
        string provider,
        [FromBody] JsonElement payload,
        CancellationToken cancellationToken)
    {
        var result = await syncEmployee.HandleAsync(
            new SyncEmployeeCommand(provider, payload),
            cancellationToken);

        return ResolveOutcome(provider, result);
    }

    private CreatedAtRouteResult CreatedEmployee(EmployeeDetails employee)
    {
        var response = EmployeeResponse.From(employee);
        return CreatedAtRoute(
            EmployeesController.GetEmployeeRouteName,
            new
            {
                version = HttpContext.RequestedApiVersion?.ToString(),
                provider = response.Provider,
                providerEmployeeId = response.ProviderEmployeeId,
            },
            response);
    }

    private ObjectResult SynchronizationFailed(EmployeeDetails employee)
    {
        var response = EmployeeResponse.From(employee);
        return this.ProblemWithExtensions(
            StatusCodes.Status502BadGateway,
            "TrackTik synchronization failed.",
            "The employee was stored but could not be synchronized to TrackTik. Resubmit to retry.",
            new Dictionary<string, object?>
            {
                ["employeeId"] = response.EmployeeId,
                ["syncStatus"] = response.SyncStatus,
                ["attempts"] = response.Attempts,
                ["lastError"] = response.LastError,
                ["lastAttemptAt"] = response.LastAttemptAt,
            });
    }

    private IActionResult ResolveOutcome(string provider, SyncEmployeeResult result)
    {
        return result.Outcome switch
        {
            SyncEmployeeOutcome.Synchronized when result.EmployeeCreated => CreatedEmployee(result.Employee!),
            SyncEmployeeOutcome.Synchronized => Ok(EmployeeResponse.From(result.Employee!)),
            SyncEmployeeOutcome.ValidationFailed => ValidationProblem(
                title: "The provider payload is invalid.",
                modelStateDictionary: result.ValidationErrors.ToModelState()),
            SyncEmployeeOutcome.UnknownProvider => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Unknown provider.",
                detail: $"Provider '{provider}' is not supported. Supported providers: provider1, provider2."),
            SyncEmployeeOutcome.SynchronizationFailed => SynchronizationFailed(result.Employee!),
            SyncEmployeeOutcome.PersistenceConflict => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Concurrent update.",
                detail: "The employee was modified by another request. Check its current state before resubmitting."),
            _ => throw new InvalidOperationException($"Unhandled synchronization outcome '{result.Outcome}'."),
        };
    }
}
