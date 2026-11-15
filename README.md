# ManagerEmployee

[![CI](https://github.com/Dalben06/manager-employee/actions/workflows/ci.yml/badge.svg)](https://github.com/Dalben06/manager-employee/actions/workflows/ci.yml)

ManagerEmployee is an ASP.NET Core Web API (.NET 10). It receives employee records from two identity providers, stores them in SQL Server through EF Core, and synchronizes them to the TrackTik REST API (mock) using OAuth2 client credentials.

Every submission follows the same path: **validate → map → store as `pending` → send to TrackTik → store as `synced` or `failed`**. A TrackTik failure returns `502` and keeps the stored record, so the employee can be retried by resubmitting it.

**Contents:** [Quick Start](#quick-start) · [Run the Project](#run-the-project) · [API](#api) · [Testing](#testing) · [Architecture and Decisions](#architecture-and-decisions) · [Configuration](#configuration) · [Assumptions and Trade-offs](#assumptions-and-trade-offs) · [What I'd Do Next](#what-id-do-next)

## Quick Start

```bash
cp .env.example .env              # then fill in the three values
docker compose up --build --wait
curl -i http://localhost:8080/openapi/v1.json
```

Then run the `curl` examples in [API](#api). A first POST returns `201`, and the GET returns the employee with `"syncStatus": "synced"`.

To try the service without TrackTik credentials, use the Postman stack with a local TrackTik stub, described in [Testing](#testing).

## Run the Project

### Prerequisites

- .NET SDK 10.0.300 or a compatible .NET 10 SDK
- Docker Desktop or another Docker Engine with Compose v2, for the container workflow and the container tests
- SQL Server when running outside Compose
- `dotnet-ef` 10.0.x to apply or create migrations
- Node.js 18+, only for the Newman end-to-end and load tests

### With Docker Compose

Compose reads its values from the shell or from an untracked `.env` file. Copy the committed template with `cp .env.example .env` and replace the placeholders:

```dotenv
SQLSERVER_SA_PASSWORD=replace-with-a-strong-local-development-password
TRACKTIK_CLIENT_ID=replace-with-your-client-id
TRACKTIK_CLIENT_SECRET=replace-with-your-client-secret
```

The `.env` file is ignored by Git and Docker. Never use the personal assessment credential file as committed configuration.

```bash
docker compose up --build --wait
docker compose ps
docker compose down
```

- The API listens on `http://localhost:8080` by default. Override the host ports with `API_PORT` and `SQLSERVER_PORT`.
- Compose uses `sqlserver` as the database hostname and waits for its health check before starting the API.
- Compose sets `Database__ApplyMigrationsOnStartup=true`, so the API applies all pending EF Core migrations when it starts. The Compose workflow needs no `dotnet-ef` installation.
- Compose supplies the connection string as `ConnectionStrings__ManagerEmployeeDatabase` and the TrackTik credentials as `TrackTik__ClientId` and `TrackTik__ClientSecret`. The TrackTik URLs come from `appsettings.json`.

### With dotnet run

```bash
dotnet user-secrets set TrackTik:ClientId "<your-client-id>" --project Source/ManagerEmployee.Api
dotnet user-secrets set TrackTik:ClientSecret "<your-client-secret>" --project Source/ManagerEmployee.Api
dotnet run --project Source/ManagerEmployee.Api/ManagerEmployee.Api.csproj
```

The Development environment uses the local SQL Server connection in `appsettings.Development.json` (`localhost,1433`, integrated security). Override it with `dotnet user-secrets set ConnectionStrings:ManagerEmployeeDatabase "<connection string>" --project Source/ManagerEmployee.Api` or the `ConnectionStrings__ManagerEmployeeDatabase` environment variable.

Startup fails with a clear message when the TrackTik credentials are missing (see [Configuration](#configuration)). With the generated launch profile, the OpenAPI document is at `http://localhost:5069/openapi/v1.json`.

### Database Migrations

Migrations are committed under `Source/ManagerEmployee.Infrastructure/Persistence/Migrations`. First install the EF Core command-line tool matching the package version:

```bash
dotnet tool install --global dotnet-ef --version 10.0.15
```

The API is the startup project, so `dotnet ef` uses the same configuration as `dotnet run`: the Development connection string, user secrets, or `ConnectionStrings__ManagerEmployeeDatabase`.

```bash
dotnet ef database update \
  --project Source/ManagerEmployee.Infrastructure/ManagerEmployee.Infrastructure.csproj \
  --startup-project Source/ManagerEmployee.Api/ManagerEmployee.Api.csproj
```

Alternatively, set `Database__ApplyMigrationsOnStartup=true` (or `Database:ApplyMigrationsOnStartup` in user secrets) before `dotnet run`, and the API applies migrations at startup, as Compose does.

### Connect With SSMS

Use SQL Server Authentication with login `sa`, check **Trust server certificate**, and open the `ManagerEmployee` database:

| Stack | Server | Password |
|---|---|---|
| Docker Compose | `localhost,1433` | `SQLSERVER_SA_PASSWORD` from `.env` |
| Postman stack (see [Testing](#testing)) | `localhost,14334` | `SQLSERVER_SA_PASSWORD` from `postman/postman.env` |

Employees are stored in `dbo.Employees`.

## API

All routes are versioned with a URL segment (`/api/v1`). Every error response is an RFC 9457 `application/problem+json` body with a `traceId`.

| Method and route | Responses |
|---|---|
| `POST /api/v1/providers/{provider}/employees` | `201` first time an employee is stored (with `Location`), `200` resubmission, `400` invalid payload (`errors` keyed by provider field), `404` unknown provider, `409` concurrent update, `415` non-JSON body, `502` TrackTik failure (body includes `syncStatus`, `employeeId`, `attempts`, `lastError`, `lastAttemptAt`) |
| `GET /api/v1/employees/{provider}/{providerEmployeeId}` | `200` stored employee with its TrackTik `employeeId`, `syncStatus`, `attempts`, `lastError`, `lastAttemptAt`; `404` unknown provider or employee |
| `GET /api/v1/reports/sync-status` | `200` stored employee counts per sync status, for example `{"pending":0,"synced":2,"failed":1,"total":3}`. Served by the T-SQL procedure `dbo.usp_GetEmployeeSyncStatusCounts` (bonus) |

`{provider}` is `provider1` or `provider2` (lowercase). Each POST body is one employee in that provider's schema. The OpenAPI document is served at `/openapi/v1.json` in every environment.

```bash
curl -i -X POST http://localhost:8080/api/v1/providers/provider1/employees \
  -H "Content-Type: application/json" \
  -d '{"emp_id":"P1_001","first_name":"Alice","last_name":"Johnson","email_address":"alice.johnson@provider1.com","phone":"+1-555-0101","job_title":"Security Officer","dept":"Security Operations","hire_date":"2024-01-15","employment_status":"active"}'

curl -i -X POST http://localhost:8080/api/v1/providers/provider2/employees \
  -H "Content-Type: application/json" \
  -d '{"employee_number":"P2_001","personal_info":{"given_name":"Carol","family_name":"Davis","email":"carol.davis@provider2.com","mobile":"+1-555-0201"},"work_info":{"role":"Security Guard","division":"Night Shift Security","start_date":"2024-02-01","current_status":"employed"}}'

curl -i http://localhost:8080/api/v1/employees/provider1/P1_001

curl -i http://localhost:8080/api/v1/reports/sync-status
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

## Testing

The full guide, with commands, the WireMock stub, the Postman collection, load testing, reports, and troubleshooting, is in **[Documentation/testing.md](Documentation/testing.md)**.

| Layer | What it proves | Command |
|---|---|---|
| Unit tests (Domain, Application, Infrastructure) | Provider mapping and every status conversion, aggregate invariants, sync order, POST vs PUT, event published only after the `synced` commit, TrackTik client (token reuse, refresh after `401`, timeouts, `5xx`) | `dotnet test ManagerEmployee.slnx` |
| API integration tests (in-memory) | HTTP pipeline, status codes, ProblemDetails, OpenAPI | `dotnet test ManagerEmployee.slnx` |
| SQL Server container tests (Testcontainers) | Migrations and procedure, composite key and `rowversion` conflicts, status constraint, report | `dotnet test ManagerEmployee.slnx` (Docker) |
| Postman end-to-end tests against WireMock | The running stack against a local TrackTik stub: mapping, POST/PUT choice, `502` and retry, token caching | `cd postman && npm run e2e` |
| Newman load test | Parallel volume; database totals match the data sent (no duplicates, nothing stuck in `pending`) | `cd postman && npm run load` |

`dotnet test ManagerEmployee.slnx` is the single command for all automated tests. It needs no network access or TrackTik credentials. The Postman and load tests run against a separate Compose stack in which a [WireMock](https://wiremock.org) container replaces TrackTik:

```bash
docker compose -p me-postman --env-file postman/postman.env \
  -f docker-compose.yml -f docker-compose.postman.yml up --build --wait
cd postman && npm ci && npm run e2e && npm run load -- --count 2000 --workers 8
```

GitHub Actions ([`.github/workflows/ci.yml`](.github/workflows/ci.yml)) runs all of this on every push to `main` and on every pull request. The first job runs restore, build, every `dotnet test` project (SQL Server included) and the format check. The second runs the Postman end-to-end suite and a 500-employee load run. See [Continuous Integration](Documentation/testing.md#5-continuous-integration).

Latest verified results:

- 107 automated tests (21 Domain, 18 Application, 46 Infrastructure, 13 API integration, 9 SQL Server container).
- Postman: 66/66 assertions.
- Load: 2,000 employees across 8 parallel workers (6,000 requests, about 115 requests/s), 0 failures, all database checks passed.

## Architecture and Decisions

The solution is a lightweight layered design with four production projects. Dependencies point inward:

```text
ManagerEmployee.Api             Controllers (transport only), versioning, ProblemDetails, OpenAPI, composition root
   └─> ManagerEmployee.Application    Use cases: SyncEmployeeCommandHandler, query handlers, ports
          └─> ManagerEmployee.Domain  Employee aggregate, identity, invariants, EmployeeSynced event
ManagerEmployee.Infrastructure  Implements the ports: EF Core + SQL Server, provider adapters, TrackTik HTTP/OAuth, in-memory events
```

```text
Documentation/                                Architecture, dependencies, testing guide, and ADRs
Source/ManagerEmployee.{Api,Application,Domain,Infrastructure}/
Tests/ManagerEmployee.{Domain,Application,Infrastructure}.Tests/
Tests/ManagerEmployee.Api.IntegrationTests/   API with the EF Core in-memory provider
Tests/ManagerEmployee.Api.ContainerTests/     API against real SQL Server (Testcontainers)
postman/                                      Postman collection, WireMock stub mappings, Newman load runner
docker-compose.yml, docker-compose.postman.yml
ManagerEmployee.slnx
```

Project directories are flat under `Source/` and `Tests/`. `ManagerEmployee.slnx` groups them into the logical solution folders `Apps` and `Services`.

See [Documentation/architecture.md](Documentation/architecture.md) for boundaries and the request sequence, and [Documentation/dependencies.md](Documentation/dependencies.md) for the package inventory.

### Decision Records

| ADR | Decision | Why |
|---|---|---|
| [0001](Documentation/decisions/0001-solution-structure.md) | Hybrid layered solution with ASP.NET Core controllers; provider DTOs, validators and mappers are internal to `Infrastructure/Providers` | Clear Domain / Application / Infrastructure / API boundaries without framework ceremony. Provider formats never leak past the adapter, so each provider can be tested in isolation. |
| [0002](Documentation/decisions/0002-employee-domain-and-persistence.md) | One normalized `Employee` aggregate and one `Employees` table; raw provider JSON is not stored | Both providers map to the same TrackTik model. The durable `HasBeenCreatedInTrackTik` flag decides POST vs PUT, because the TrackTik mock keeps no state and cannot say whether an employee exists. |
| [0003](Documentation/decisions/0003-direct-cqrs-domain-events-and-composite-identity.md) | Direct command/query handlers (no MediatR); composite key `(provider, providerEmployeeId)`; `EmployeeSynced` published after the `synced` commit; no transaction held across the TrackTik call | One write and one read use case don't justify a dispatcher. The composite key enforces the contractual identity in the database. Publishing after the commit means an event is never raised for state that wasn't saved. Not holding locks during a remote call keeps the database free while TrackTik responds. |
| [0004](Documentation/decisions/0004-sync-status-report-procedure.md) | Sync-status report served by the T-SQL procedure `dbo.usp_GetEmployeeSyncStatusCounts` | Bonus requirement. The procedure is created by a migration, called with a parameterized query, and leaves the EF model unchanged. |

### Rules the Design Protects

- Employee identity is `(provider, providerEmployeeId)`. The TrackTik id is always `{provider}-{providerEmployeeId}`. Records from different providers are never merged, even when they share an email.
- The order is fixed: validate, map, commit `pending`, call TrackTik, then commit `synced` or `failed`. Invalid payloads are neither stored nor forwarded.
- A TrackTik failure keeps the record as `failed` and returns `502` with the sync status. A resubmission updates the same row and retries.
- `EmployeeSynced` is published exactly once per successful sync, only after the `synced` commit.

## Configuration

All TrackTik settings live in the `TrackTik` configuration section. They can come from any .NET configuration source: `appsettings.json`, user secrets, or environment variables.

| Key | Environment variable | Default |
|---|---|---|
| `TrackTik:ApiBaseUrl` | `TrackTik__ApiBaseUrl` | The assessment mock `/v1` URL (in `appsettings.json`) |
| `TrackTik:TokenUrl` | `TrackTik__TokenUrl` | The assessment mock OAuth2 token URL (in `appsettings.json`); it is not under `/v1` |
| `TrackTik:ClientId` | `TrackTik__ClientId` | Empty: required |
| `TrackTik:ClientSecret` | `TrackTik__ClientSecret` | Empty: required |

The settings are validated at startup: both URLs must be absolute and both credentials must be set. Supply the credentials with user secrets (see [With dotnet run](#with-dotnet-run)) or environment variables. Never put real values in committed `appsettings*.json` or Compose files. Committed configuration contains placeholders only.

The client:

- requests a token with a form-encoded `client_credentials` grant, without `scope`,
- caches the token until shortly before `expires_in` (60 seconds before, or halfway through the lifetime for tokens shorter than two minutes),
- on a `401`, refreshes the token and retries once.

TrackTik errors, timeouts (10 seconds) and token failures are recorded as `failed` with a safe `lastError`, for example `TrackTik returned 400 VALIDATION_ERROR: Invalid employee data (email: Invalid email format)`. Credentials and tokens are never included.

## Assumptions and Trade-offs

**Design**

- Controllers are chosen over minimal APIs to keep transport boundaries explicit.
- The architecture stays lightweight: no mediator, generic repository, AutoMapper, or event-bus framework without a concrete need. Commands and queries use directly injected handlers.
- Provider DTOs are `internal` to `Infrastructure/Providers`. Application receives only normalized records, and Domain receives only domain values.
- Domain owns the composite identity, the aggregate repository abstraction, the field policy, and the typed `EmployeeSynced` event. Infrastructure owns the EF Core write and read adapters.
- Bonus work done after the mandatory scope:
  - command/query separation,
  - a T-SQL report procedure,
  - Testcontainers integration tests.

  NATS was not attempted. The in-memory publisher covers the mandatory event behavior.

**API behavior**

- `POST` returns `201` when the employee is stored for the first time and `200` on resubmission, regardless of whether TrackTik received a POST or a PUT.
- A missing employment status (`employment_status` / `work_info.current_status`) is rejected with `400`. The provider schemas make it optional, but TrackTik requires `status`.
- Provider route values are exact and lowercase. Anything else returns `404`.

**Concurrency and durability**

- Same-identity requests are serialized within one API instance. A concurrent update detected by the database returns `409`.
- With several instances, the final `synced` commit can conflict after TrackTik has already accepted a POST. Local state then still says the employee was never created, so the next submission POSTs again. Closing that window would need a distributed lock or an outbox, which is outside the mandatory scope.
- Once the `pending` commit succeeds, the sync finishes even if the client disconnects. The stored status therefore never stays `pending` after TrackTik has accepted the employee.
- The aggregate creates `EmployeeSynced`, and Application publishes it in memory only after the `synced` commit. Delivering events durably across process failures would need an outbox.

**TrackTik calls**

- Calls are not retried automatically, apart from the single token refresh after a `401`. A failed employee is retried by resubmitting it, as the contract describes.
- One submission makes at most four sequential TrackTik calls (token, request, then one token refresh and retry after `401`), each with a 10-second timeout.
- Observability (structured logging dimensions and metrics) is not implemented yet.

## What I'd Do Next

- **Durable events**: replace the in-memory publisher with an outbox table written in the same commit as `synced`, relayed to NATS or another broker. This would survive a crash between the commit and publication.
- **Multi-instance safety**: replace the process-local coordinator with a distributed lock (for example `sp_getapplock` or Redis) or a per-identity queue, so concurrent instances cannot race on the final commit.
- **Resilience**: add a bounded retry with jitter for idempotent TrackTik `PUT`s on `429`/`5xx` (honouring `Retry-After`), and a circuit breaker so an outage fails fast.
- **Observability**: add structured logs with the employee identity and trace id, metrics for sync outcomes and TrackTik latency, and OpenTelemetry export.
- **Background reconciliation**: add a job that retries `failed` employees on a schedule, instead of waiting for a provider resubmission.
