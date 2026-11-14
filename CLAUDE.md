# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

@AGENTS.md

`AGENTS.md` (imported above) holds the canonical commands, contract traps, and secret-handling rules. Treat it as binding; this file adds only orientation that it does not cover.

## What This Is

A .NET 10 assessment solution (deadline Nov 16, 2026): an Employee Sync Service that accepts employee submissions from two providers, validates and maps them, persists sync state in SQL Server via EF Core, and forwards them to a TrackTik mock REST API. `prompts/requirements.md` is the requirement ledger (IDs, current phase, in/out of scope, completion evidence). `Documentation/architecture.md` and the ADRs in `Documentation/decisions/` explain why things are the way they are. Read them before changing boundaries.

## Request Flow Across Projects

The sync path spans every project, so it helps to know where each step lives:

1. **API** (`Source/ManagerEmployee.Api`): `Program.cs` composes `AddApi()` (controllers, Asp.Versioning URL segments, versioned OpenAPI, ProblemDetails with `traceId`; unhandled exceptions use the built-in `UseExceptionHandler` 500), `AddApplication()` (handlers, coordinator, `TimeProvider`), and `AddInfrastructure(configuration)`. Startup requires the `ManagerEmployeeDatabase` connection string (in `appsettings.Development.json` for local runs) and valid `TrackTik` options (URLs in `appsettings.json`, credentials from user secrets or environment, validated on start); `Database:ApplyMigrationsOnStartup` (set by Compose) applies EF migrations. `ProviderEmployeesController` and `EmployeesController` are transport-only: each maps the handler outcome to one status in a single switch, and `EmployeeResponse` is the only response contract. Integration tests use `ManagerEmployeeApiFactory` (EF InMemory + `RowVersionInterceptor` + `FakeTrackTikEmployeeClient`).
2. **Providers** (`Infrastructure/Providers/Provider1|Provider2`) each hold `internal` provider DTOs, a FluentValidation validator built from the shared rules in `ProviderFieldRules` (errors use the provider's JSON field names), and an `IProviderEmployeeMapper` that returns the Application-owned `MappedEmployee`. Provider DTOs never leave Infrastructure. Mappers are registered in `AddInfrastructure`.
3. **Application**: `SyncEmployeeCommandHandler` is the single orchestrator. It parses the provider route value with `EmployeeProviderExtensions.TryParseContractValue`, picks the matching injected mapper, validates and maps, acquires a per-identity lease from `EmployeeSynchronizationCoordinator` (64 striped semaphores, process-local only), loads or creates the aggregate, commits `pending`, calls `ITrackTikEmployeeClient` (POST when `!employee.HasBeenCreatedInTrackTik`, otherwise PUT), commits `failed` or `synced`, and only then publishes `EmployeeSynced`. Results are returned as `SyncEmployeeResult` outcomes (unknown provider, validation failed, persistence conflict, sync failed, synchronized), not as exceptions. Reads go through `GetEmployeeQueryHandler` → `IEmployeeReadStore`, which returns an immutable `EmployeeDetails`.
4. **Domain**: the `Employee` aggregate (identity `EmployeeIdentity`, `EmployeeProfile`, `EmployeeSynchronizationAttempt`) enforces state transitions. `MarkSynced` *creates* the `EmployeeSynced` event but does not publish it. `IEmployeeRepository.CommitAsync` returns `EmployeeCommitOutcome` (conflict vs. success). Field limits live in `Policies/EmployeeFieldLimits`.
5. **Infrastructure**: `ManagerEmployeeDbContext` uses the composite PK `(provider, providerEmployeeId)`, lowercase status check constraints, and a `rowversion` concurrency token. `EmployeeRepository` is the tracked writer and translates concurrency errors into `Conflict`. `EmployeeReadStore` uses `AsNoTracking` projections. `InMemoryEmployeeSyncedEventPublisher` invokes typed handlers sequentially. `TrackTik/TrackTikEmployeeClient` (typed `HttpClient`) sends POST/PUT with a bearer token from the singleton `TrackTikTokenProvider` (form-encoded client credentials, cached until 60s before expiry, one refresh-and-retry after `401`) and maps every remote failure to a safe `TrackTikSyncResult.Failure`.

## Migrations

Migrations are in `Source/ManagerEmployee.Infrastructure/Persistence/Migrations`. There is no design-time factory: the API is the `--startup-project`, so `dotnet ef` reads the same configuration as `dotnet run`:

```bash
dotnet ef migrations add <Name> \
  --project Source/ManagerEmployee.Infrastructure/ManagerEmployee.Infrastructure.csproj \
  --startup-project Source/ManagerEmployee.Api/ManagerEmployee.Api.csproj \
  --output-dir Persistence/Migrations
```

Override the connection with `ConnectionStrings__ManagerEmployeeDatabase` when the default local integrated-security connection doesn't fit. `dotnet-ef` must be 10.0.x (README pins 10.0.15).

## Build Settings That Bite

`Directory.Build.props` sets `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, and `Nullable=enable`. `.editorconfig` violations therefore break `dotnet build`, not just `dotnet format`. Package versions are centralized in `Directory.Packages.props`, so add `<PackageReference>` without a `Version` attribute.

## Non-Source Directories

- `examples/AbcCompany` and `examples/EquinoxProject` are reference repositories for the aggregate-repository style. They are not part of `ManagerEmployee.slnx`; don't modify or build them.
- `docs/superpowers/{plans,specs}` holds earlier planning artifacts. `prompts/` holds the design prompt and requirements ledger.
- `interview-detail/` is git-ignored and contains the original challenge spec, JSON schemas, sample data, and personal OAuth credentials (see the AGENTS.md secret rules).
