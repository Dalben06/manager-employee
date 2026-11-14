# Repository Instructions

## Contract And Evidence

- Read `prompts/requirements.md` before implementation work and name the requirement IDs being addressed.
- Treat `interview-detail/README.md`, `interview-detail/API_DOCUMENTATION.md`, `interview-detail/SUBMISSION.md`, `interview-detail/schemas/*.json`, and `interview-detail/sample-data/*.json` as authoritative when the requirements ledger is unclear.
- Update requirement checkboxes and `Completion Evidence` only after verification succeeds; record the IDs, changed files, and exact successful command or test result.
- Complete and verify mandatory requirements before attempting anything under `Bonus Requirements`.

## Structure And Current State

- Provider validation and mapping, synchronization orchestration, in-memory events, the normalized domain model, EF Core persistence, and migrations are implemented. The versioned employee API and the OAuth2 TrackTik HTTP client are implemented.
- Keep the established dependency direction: Domain owns aggregates, invariants, validation, and aggregate repository abstractions; Application owns use cases and external-system/event ports without ASP.NET Core or EF Core; Infrastructure owns EF Core, HTTP/OAuth, provider, and event adapters; API is the composition root and keeps controllers transport-only.
- Controllers are intentional. Do not add MediatR, a custom dispatcher, AutoMapper, a generic repository, or an event-bus framework without a concrete requirement.
- The provider schemas describe one employee object. The files under `sample-data/` are arrays of fixtures, not a documented batch-ingestion contract.

## Commands

- Run from the repository root in this order: `dotnet restore ManagerEmployee.slnx`, `dotnet build ManagerEmployee.slnx --no-restore`, `dotnet test ManagerEmployee.slnx --no-build`, then `dotnet format ManagerEmployee.slnx --verify-no-changes`. Warnings and code-style violations fail the build.
- `--no-build` assumes this checkout was already built; after moving the workspace, rebuild first to replace stale binaries that may contain old absolute paths.
- Run one test project with `dotnet test <test-project.csproj> --no-build`; focus a test with `--filter "FullyQualifiedName~TestMethodName"`.
- All test projects enable MSTest method-level parallelism; isolate shared resources or use `[DoNotParallelize]` for tests that cannot run concurrently.
- Projects are physically flat under `Source/` and `Tests/`; `Apps` and `Services` exist only as logical folders in `ManagerEmployee.slnx`.
- Run the API with `dotnet run --project Source/ManagerEmployee.Api/ManagerEmployee.Api.csproj`; `/openapi/v1.json` is mapped in every environment.
- Compose commands require `SQLSERVER_SA_PASSWORD`, `TRACKTIK_CLIENT_ID`, and `TRACKTIK_CLIENT_SECRET`. The TrackTik URLs default to the mock in `appsettings.json`; the API refuses to start without `TrackTik:ClientId`/`ClientSecret` (user secrets or environment).

## Contract Traps

- Employee identity is the composite `(provider, providerEmployeeId)`; derive `{provider}-{providerEmployeeId}` and never merge across providers, even by email.
- Preserve the required sequence: validate, map, commit `pending`, call TrackTik, then commit `synced` or `failed`. A TrackTik failure must leave the record committed as `failed` and return `502 ProblemDetails` containing the sync status.
- Resubmission updates the existing row and retries. Choose TrackTik POST only when local persisted state says the employee has never synchronized successfully; use PUT after any prior successful creation. The mock does not retain records and its GET always returns sample data, so it cannot determine existence.
- Publish `EmployeeSynced` exactly once per successful sync, and only after the `synced` status commit succeeds.
- Provider 2's JSON schema requires the `personal_info` and `work_info` objects but does not mark their nested fields required. Validation must still ensure every required TrackTik output field is present and valid before persistence.
- Configure the OAuth token URL separately from the `/v1` API base URL. Token requests are form-encoded; omit `scope` unless deliberately requesting an allowed scope, cache tokens until shortly before expiry, and safely refresh after `401`.
- Test TrackTik failures with a fake `HttpMessageHandler` or stub server: the hosted mock does not simulate outages and is rate-limited.

## Secrets And Submission

- `interview-detail/` is intentionally ignored because `oauth-credentials.json` contains personal credentials. Never force-add it or copy credential values into source, logs, errors, tests, appsettings, or Compose files.
- Commit only placeholders for OAuth and non-local passwords. Before submission, inspect repository history/content for secrets; the final GitHub repository must be public.
