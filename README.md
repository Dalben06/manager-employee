# ManagerEmployee

ManagerEmployee is a .NET 10 Employee Sync Service assessment solution. The service layer uses a focused employee aggregate, direct command/query handlers, a Domain-owned write repository, an Application-owned no-tracking read-store port, EF Core persistence, provider validation and mapping, synchronization orchestration, typed post-commit events, SQL Server migrations, and process-local same-identity coordination. Employee API endpoints and the concrete TrackTik HTTP/OAuth client are not implemented yet.

## Prerequisites

- .NET SDK 10.0.300 or a compatible .NET 10 SDK
- Docker Desktop or another Docker Engine with Compose v2 for the container workflow
- SQL Server when running outside Compose in a later implementation phase
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

See `Documentation/architecture.md` for boundaries and the future synchronization contract.

## Restore, Build, and Test

Run commands from the repository root:

```bash
dotnet restore ManagerEmployee.slnx
dotnet build ManagerEmployee.slnx --no-restore
dotnet test ManagerEmployee.slnx --no-build
dotnet format ManagerEmployee.slnx --verify-no-changes
```

`dotnet test ManagerEmployee.slnx` is the single command for all automated tests. The current tests cover employee invariants, shared field policies, both provider mappings, command ordering and state transitions, query behavior, immutable read models, same-identity coordination, event timing and publication, persistence conflicts, no-tracking EF Core reads, model constraints, and the Development OpenAPI document.

## Run Directly

```bash
dotnet run --project Source/ManagerEmployee.Api/ManagerEmployee.Api.csproj
```

The Development OpenAPI document is available at `http://localhost:5069/openapi/v1.json` with the generated launch profile. Employee endpoints are not yet exposed.

## Docker Compose

Compose requires values to be supplied through the shell or an untracked `.env` file:

```dotenv
SQLSERVER_SA_PASSWORD=replace-with-a-strong-local-development-password
TRACKTIK_API_BASE_URL=https://example.invalid/v1
TRACKTIK_TOKEN_URL=https://example.invalid/oauth/token
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

The API consumes `ConnectionStrings:ManagerEmployeeDatabase`; Compose supplies it through `ConnectionStrings__ManagerEmployeeDatabase`. The TrackTik values establish the configuration contract for the later HTTP integration and are not consumed by a concrete client yet.

## TrackTik Configuration

Future TrackTik integration will read these configuration values from environment variables, .NET user secrets, or another untracked provider:

| Environment variable | Purpose |
|---|---|
| `TRACKTIK_API_BASE_URL` | TrackTik `/v1` API base URL |
| `TRACKTIK_TOKEN_URL` | Separate OAuth2 token URL |
| `TRACKTIK_CLIENT_ID` | OAuth2 client ID |
| `TRACKTIK_CLIENT_SECRET` | OAuth2 client secret |

For direct .NET configuration, the corresponding hierarchical keys will be `TrackTik:ApiBaseUrl`, `TrackTik:TokenUrl`, `TrackTik:ClientId`, and `TrackTik:ClientSecret`. Do not put real values in committed `appsettings*.json` files.

## Database Migrations

Install the EF Core command-line tool matching the repository package version:

```bash
dotnet tool install --global dotnet-ef --version 10.0.15
```

Migrations are committed under `Source/ManagerEmployee.Infrastructure/Persistence/Migrations`. Supply `ConnectionStrings__ManagerEmployeeDatabase` through the environment when the default local integrated-security connection is not appropriate, then apply migrations with:

```bash
dotnet ef database update \
  --project Source/ManagerEmployee.Infrastructure/ManagerEmployee.Infrastructure.csproj \
  --startup-project Source/ManagerEmployee.Infrastructure/ManagerEmployee.Infrastructure.csproj
```

Alternatively, set `Database__ApplyMigrationsOnStartup=true` (or `Database:ApplyMigrationsOnStartup` in user secrets) before `dotnet run` to have the API apply migrations at startup, as Compose does.

The Infrastructure project supplies a design-time context factory. At runtime, the API remains the composition root and registers the same SQL Server context and employee repository.

## Assumptions and Deferred Work

- Controllers are selected over minimal APIs to make transport boundaries explicit.
- The architecture stays lightweight: no mediator, generic repository, AutoMapper, or event-bus framework is introduced without a concrete need.
- Commands and queries use directly injected handlers; no command/query dispatcher or marker hierarchy is used.
- Mandatory behavior is implemented and verified before any bonus requirement.
- The persistence representation and initial concurrency safeguards are documented in ADRs 0002 and 0003. Same-identity requests are serialized within one process; cross-instance races return an explicit persistence-conflict outcome. Token refresh details and observability dimensions remain for later implementation prompts.
- Provider DTOs remain internal to their integration projects; Application receives only normalized records and Domain receives only domain values.
- Domain owns the composite employee identity, aggregate repository abstraction, field policy, cohesive profile/attempt models, and typed `EmployeeSynced` event; Infrastructure owns the EF Core write and read adapters.
- The aggregate creates `EmployeeSynced`, and Application publishes it in memory only after the `synced` commit. Durable delivery across process failures would require an outbox and is outside the mandatory in-memory implementation.
- The employee-sync API and concrete TrackTik/OAuth workflow remain incomplete even though provider mapping and application orchestration now exist.
