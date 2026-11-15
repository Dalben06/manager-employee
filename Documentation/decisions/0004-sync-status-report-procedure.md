# ADR 0004: Serve the Sync-Status Report From a T-SQL Stored Procedure

- Status: Accepted
- Date: 2026-11-13

## Context

The challenge offers a bonus for "a T-SQL stored procedure or report query (for example, employee counts by sync status)". Operators also benefit from a quick view of how many employees are `pending`, `synced`, or `failed`. EF Core parameterizes every query, and the challenge forbids string-built SQL.

## Decision

- Add the read-only procedure `dbo.usp_GetEmployeeSyncStatusCounts` (`SELECT SyncStatus, COUNT(*) ... GROUP BY SyncStatus`) through the `AddEmployeeSyncStatusReport` EF Core migration. `Down` drops it. The EF model is unchanged.
- Expose it as a second query use case: the Application port `IEmployeeSyncStatusReportReader`, `GetSyncStatusReportQueryHandler`, and the immutable `SyncStatusReport` model. The handler returns zero for any status with no employees.
- Implement the port in Infrastructure with `Database.SqlQuery<T>($"EXEC dbo.usp_GetEmployeeSyncStatusCounts")`. That is an interpolated `FormattableString` with no user input, so it stays parameterized.
- Serve it from `GET /api/v1/reports/sync-status` in a transport-only `ReportsController`.

## Consequences

- The procedure is versioned with the schema and applied wherever migrations run, including Compose startup.
- The EF Core in-memory provider cannot execute it, so the in-memory API tests fake the reader. The Testcontainers suite runs the real procedure against SQL Server.
- The handler has no parameters, so it takes no query object, in keeping with the direct-handler style of ADR 0003.

## Rejected Alternatives

### LINQ `GroupBy` in the Read Store

This would work without a procedure, but it would not meet the bonus's explicit T-SQL request.

### A Database View Mapped as a Keyless Entity

This was rejected because it adds an entity type to the model for a single aggregate query.
