# ManagerEmployee

ManagerEmployee is an ASP.NET Core Web API (.NET 10) that receives employee records from two identity providers, stores them in SQL Server through EF Core, and synchronizes them to the TrackTik REST API (mock) using OAuth2 client credentials.

Each submission is validated, mapped, stored as `pending`, sent to TrackTik, and then stored as `synced` or `failed`. A TrackTik failure returns `502` and keeps the stored record.

## Quick Start

```bash
cp .env.example .env              # then fill in the three values
docker compose up --build --wait
curl -i http://localhost:8080/openapi/v1.json
```

Then run the `curl` examples in [API](#api): a first POST returns `201`, and the GET returns the employee with `"syncStatus": "synced"`.

## Prerequisites

- .NET SDK 10.0.300 or a compatible .NET 10 SDK
- Docker Desktop or another Docker Engine with Compose v2 for the container workflow
- SQL Server when running outside Compose
- `dotnet-ef` 10.0.x to apply or create migrations

## Structure

```text
Documentation/                         Architecture, dependencies, and ADRs
Source/ManagerEmployee.Api/            ASP.NET Core host and composition root
Source/ManagerEmployee.Application/
Source/ManagerEmployee.Domain/
Source/ManagerEmployee.Infrastructure/  EF Core, provider adapters (Providers/), events
Tests/ManagerEmployee.Api.IntegrationTests/
Tests/ManagerEmployee.Application.Tests/
Tests/ManagerEmployee.Domain.Tests/
Tests/ManagerEmployee.Infrastructure.Tests/
ManagerEmployee.slnx
```

The physical project directories are flat under `Source/` and `Tests/`. `ManagerEmployee.slnx` groups them into logical `Apps` and `Services` solution folders.

See `Documentation/architecture.md` for boundaries and the synchronization contract.

## Restore, Build, and Test

Run commands from the repository root:

```bash
dotnet restore ManagerEmployee.slnx
dotnet build ManagerEmployee.slnx --no-restore
dotnet test ManagerEmployee.slnx --no-build
dotnet format ManagerEmployee.slnx --verify-no-changes
```

`dotnet test ManagerEmployee.slnx` is the single command for all automated tests. No database, Docker, or network access is needed: TrackTik is replaced by a fake `HttpMessageHandler` or a fake client, and the API integration tests (`WebApplicationFactory`) use the EF Core in-memory provider.

The tests cover:

- both provider mappings, including every status conversion
- employee invariants and the sync order (pending, then TrackTik, then synced or failed), including POST for new or previously failed employees and PUT after a successful sync
- event publication only after the `synced` commit
- persistence conflicts and same-identity coordination
- the TrackTik client: token reuse, refresh after `401`, timeouts, `5xx`, and token failures
- the versioned API routes, the ProblemDetails responses (`400`, `404`, `502`), and the OpenAPI document

## Run Directly

```bash
dotnet user-secrets set TrackTik:ClientId "<your-client-id>" --project Source/ManagerEmployee.Api
dotnet user-secrets set TrackTik:ClientSecret "<your-client-secret>" --project Source/ManagerEmployee.Api
dotnet run --project Source/ManagerEmployee.Api/ManagerEmployee.Api.csproj
```

The Development environment uses the local SQL Server connection in `appsettings.Development.json` (`localhost,1433`, integrated security). Override it with `dotnet user-secrets set ConnectionStrings:ManagerEmployeeDatabase "<connection string>" --project Source/ManagerEmployee.Api` or the `ConnectionStrings__ManagerEmployeeDatabase` environment variable. Startup fails with a clear message when the TrackTik credentials are missing (see [TrackTik Configuration](#tracktik-configuration)).

The OpenAPI document is available at `http://localhost:5069/openapi/v1.json` with the generated launch profile (`http://localhost:8080/openapi/v1.json` under Compose).

## Docker Compose

Compose requires values to be supplied through the shell or an untracked `.env` file. Copy the committed template with `cp .env.example .env` and replace the placeholders:

```dotenv
SQLSERVER_SA_PASSWORD=replace-with-a-strong-local-development-password
TRACKTIK_CLIENT_ID=replace-with-your-client-id
TRACKTIK_CLIENT_SECRET=replace-with-your-client-secret
```

The `.env` file is ignored by Git and Docker. Never use the personal assessment credential file as committed configuration.

Start and stop the services:

```bash
docker compose up --build --wait
docker compose ps
docker compose down
```

The API listens on `http://localhost:8080` by default. Override local host ports with `API_PORT` and `SQLSERVER_PORT`. Compose uses `sqlserver` as the database hostname and waits for its health check before starting the API.

Compose sets `Database__ApplyMigrationsOnStartup=true`, so the API applies all pending EF Core migrations when it starts. No `dotnet-ef` installation is needed for the Compose workflow.

The API consumes `ConnectionStrings:ManagerEmployeeDatabase`; Compose supplies it through `ConnectionStrings__ManagerEmployeeDatabase` and passes the TrackTik credentials as `TrackTik__ClientId` and `TrackTik__ClientSecret`. The TrackTik URLs come from `appsettings.json`.

## API

All routes are versioned with a URL segment (`/api/v1`). Every error response is an RFC 9457 `application/problem+json` body with a `traceId`.

| Method and route | Responses |
|---|---|
| `POST /api/v1/providers/{provider}/employees` | `201` first time an employee is stored (with `Location`), `200` resubmission, `400` invalid payload (`errors` keyed by provider field), `404` unknown provider, `409` concurrent update, `415` non-JSON body, `502` TrackTik failure (body includes `syncStatus`, `employeeId`, `attempts`, `lastError`, `lastAttemptAt`) |
| `GET /api/v1/employees/{provider}/{providerEmployeeId}` | `200` stored employee with its TrackTik `employeeId`, `syncStatus`, `attempts`, `lastError`, `lastAttemptAt`; `404` unknown provider or employee |

`{provider}` is `provider1` or `provider2` (lowercase). Each POST body is one employee in that provider's schema.

```bash
curl -i -X POST http://localhost:8080/api/v1/providers/provider1/employees \
  -H "Content-Type: application/json" \
  -d '{"emp_id":"P1_001","first_name":"Alice","last_name":"Johnson","email_address":"alice.johnson@provider1.com","phone":"+1-555-0101","job_title":"Security Officer","dept":"Security Operations","hire_date":"2024-01-15","employment_status":"active"}'

curl -i -X POST http://localhost:8080/api/v1/providers/provider2/employees \
  -H "Content-Type: application/json" \
  -d '{"employee_number":"P2_001","personal_info":{"given_name":"Carol","family_name":"Davis","email":"carol.davis@provider2.com","mobile":"+1-555-0201"},"work_info":{"role":"Security Guard","division":"Night Shift Security","start_date":"2024-02-01","current_status":"employed"}}'

curl -i http://localhost:8080/api/v1/employees/provider1/P1_001
```

Example `GET` response:

```json
{
  "provider": "provider1",
  "providerEmployeeId": "P1_001",
  "employeeId": "provider1-P1_001",
  "firstName": "Alice",
  "lastName": "Johnson",
  "email": "alice.johnson@provider1.com",
  "phoneNumber": "+1-555-0101",
  "position": "Security Officer",
  "department": "Security Operations",
  "startDate": "2024-01-15",
  "status": "active",
  "syncStatus": "synced",
  "attempts": 1,
  "lastError": null,
  "lastAttemptAt": "2026-11-13T01:16:48.45+00:00"
}
```

## TrackTik Configuration

All TrackTik settings live in the `TrackTik` configuration section and can come from any .NET configuration source (`appsettings.json`, user secrets, environment variables):

| Key | Environment variable | Default |
|---|---|---|
| `TrackTik:ApiBaseUrl` | `TrackTik__ApiBaseUrl` | The assessment mock `/v1` URL (in `appsettings.json`) |
| `TrackTik:TokenUrl` | `TrackTik__TokenUrl` | The assessment mock OAuth2 token URL (in `appsettings.json`); it is not under `/v1` |
| `TrackTik:ClientId` | `TrackTik__ClientId` | Empty: required |
| `TrackTik:ClientSecret` | `TrackTik__ClientSecret` | Empty: required |

The settings are validated at startup: both URLs must be absolute and both credentials must be set. Supply the credentials with user secrets (see [Run Directly](#run-directly)) or environment variables, and never put real values in committed `appsettings*.json` files.

The client requests a token with a form-encoded `client_credentials` grant (no `scope`), caches it until 60 seconds before `expires_in`, and on a `401` refreshes the token and retries once. TrackTik errors, timeouts (10 seconds), and token failures are recorded as `failed` with a safe `lastError`, for example `TrackTik returned 400 VALIDATION_ERROR: Invalid employee data (email: Invalid email format)`; credentials and tokens are never included.

## Database Migrations

Install the EF Core command-line tool matching the repository package version:

```bash
dotnet tool install --global dotnet-ef --version 10.0.15
```

Migrations are committed under `Source/ManagerEmployee.Infrastructure/Persistence/Migrations`. The API is the startup project, so `dotnet ef` uses the same configuration as `dotnet run` (the Development connection string, user secrets, or `ConnectionStrings__ManagerEmployeeDatabase`):

```bash
dotnet ef database update \
  --project Source/ManagerEmployee.Infrastructure/ManagerEmployee.Infrastructure.csproj \
  --startup-project Source/ManagerEmployee.Api/ManagerEmployee.Api.csproj
```

Alternatively, set `Database__ApplyMigrationsOnStartup=true` (or `Database:ApplyMigrationsOnStartup` in user secrets) before `dotnet run` to have the API apply migrations at startup, as Compose does.

## Assumptions and Deferred Work

- Controllers are selected over minimal APIs to make transport boundaries explicit.
- The architecture stays lightweight: no mediator, generic repository, AutoMapper, or event-bus framework is introduced without a concrete need.
- Commands and queries use directly injected handlers; no command/query dispatcher or marker hierarchy is used.
- Mandatory behavior is implemented and verified before any bonus requirement.
- The persistence representation and initial concurrency safeguards are documented in ADRs 0002 and 0003. Same-identity requests are serialized within one process; cross-instance races return an explicit persistence-conflict outcome. Observability (structured logging dimensions and metrics) is not implemented yet.
- Provider DTOs are `internal` to `Infrastructure/Providers`; Application receives only normalized records and Domain receives only domain values.
- Domain owns the composite employee identity, aggregate repository abstraction, field policy, cohesive profile/attempt models, and typed `EmployeeSynced` event; Infrastructure owns the EF Core write and read adapters.
- The aggregate creates `EmployeeSynced`, and Application publishes it in memory only after the `synced` commit. Durable delivery across process failures would require an outbox and is outside the mandatory in-memory implementation.
- TrackTik calls are not retried automatically beyond the single token refresh after `401`; a failed employee is retried by resubmitting it, as the contract describes.
- A missing employment status (`employment_status` / `work_info.current_status`) is rejected with `400`: the provider schemas make it optional, but TrackTik requires `status`.
- Provider route values are exact and lowercase; anything else returns `404`.
- `POST` returns `201` when the employee is stored for the first time and `200` on resubmission, regardless of the TrackTik POST/PUT choice.
- A concurrent update detected by the database returns `409`. Same-identity requests are serialized only within one API instance. With several instances, a conflict on the final `synced` commit after TrackTik accepted a POST leaves local state saying the employee was never created, so the next submission POSTs again. Removing that window would need a distributed lock or an outbox, which is outside the mandatory scope.
- One submission makes at most four sequential TrackTik calls (token, request, and one token refresh plus retry after `401`), each with a 10-second timeout.
- Once the `pending` commit succeeds, the sync finishes even if the client disconnects, so the stored status never stays `pending` after TrackTik has accepted the employee.
