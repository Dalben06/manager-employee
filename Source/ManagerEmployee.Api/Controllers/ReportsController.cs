using Asp.Versioning;
using ManagerEmployee.Api.Contracts;
using ManagerEmployee.Application.Employees.Queries.GetSyncStatusReport;
using Microsoft.AspNetCore.Mvc;

namespace ManagerEmployee.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/reports")]
public sealed class ReportsController(GetSyncStatusReportQueryHandler getSyncStatusReport) : ControllerBase
{
    /// <summary>Returns the number of stored employees per synchronization status.</summary>
    [HttpGet("sync-status")]
    [ProducesResponseType<SyncStatusReportResponse>(StatusCodes.Status200OK, "application/json")]
    public async Task<IActionResult> GetSyncStatus(CancellationToken cancellationToken)
    {
        var report = await getSyncStatusReport.HandleAsync(cancellationToken);
        return Ok(SyncStatusReportResponse.From(report));
    }
}
