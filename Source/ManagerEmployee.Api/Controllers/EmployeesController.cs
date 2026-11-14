using Asp.Versioning;
using ManagerEmployee.Api.Contracts;
using ManagerEmployee.Application.Employees.Queries.GetEmployee;
using ManagerEmployee.Domain.Employees.Enums;
using Microsoft.AspNetCore.Mvc;

namespace ManagerEmployee.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/employees")]
public sealed class EmployeesController(GetEmployeeQueryHandler getEmployee) : ControllerBase
{
    public const string GetEmployeeRouteName = "GetEmployee";

    /// <summary>Returns a stored employee and its TrackTik synchronization state.</summary>
    [HttpGet("{provider}/{providerEmployeeId}", Name = GetEmployeeRouteName)]
    [ProducesResponseType<EmployeeResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Get(
        string provider,
        string providerEmployeeId,
        CancellationToken cancellationToken)
    {
        if (!EmployeeProviderExtensions.TryParseContractValue(provider, out var parsedProvider))
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Unknown provider.",
                detail: $"Provider '{provider}' is not supported. Supported providers: provider1, provider2.");
        }

        var employee = await getEmployee.HandleAsync(
            new GetEmployeeQuery(parsedProvider, providerEmployeeId),
            cancellationToken);

        return employee is null
            ? Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Employee not found.",
                detail: $"No employee '{providerEmployeeId}' exists for provider '{provider}'.")
            : Ok(EmployeeResponse.From(employee));
    }
}
