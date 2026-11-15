# Testing Guide

The service is tested at five layers. The first three run with `dotnet test` and need no network access or TrackTik credentials. The last two drive the real running stack (API + SQL Server in Docker) through Postman/Newman, with TrackTik replaced by a local WireMock stub.

| Layer | What it proves | How to run |
|---|---|---|
| Unit tests (Domain, Application, Infrastructure) | Provider mapping, aggregate invariants, sync order, event timing, the TrackTik/OAuth client | `dotnet test` |
| API integration tests (in-memory) | The full HTTP pipeline, ProblemDetails, OpenAPI | `dotnet test` |
| SQL Server container tests | Migrations, composite key, `rowversion`, check constraint, report procedure | `dotnet test` (needs Docker) |
| Postman end-to-end tests against WireMock | The deployed stack: mapping, POST vs PUT choice, failure and retry, token handling | `npm run e2e` |
| Newman load test | Volume and concurrency; database totals match the data sent | `npm run load` |

Contents:

1. [Automated tests](#1-automated-tests)
2. [End-to-end with Postman + WireMock](#2-end-to-end-with-postman--wiremock)
3. [Load testing with Newman](#3-load-testing-with-newman)
4. [Teardown and troubleshooting](#4-teardown-and-troubleshooting)
5. [Continuous integration](#5-continuous-integration)

## 1. Automated Tests

Run the commands from the repository root, in this order:

```bash
dotnet restore ManagerEmployee.slnx
dotnet build ManagerEmployee.slnx --no-restore
dotnet test ManagerEmployee.slnx --no-build
dotnet format ManagerEmployee.slnx --verify-no-changes
```

`dotnet test ManagerEmployee.slnx` is the single command for all automated tests. TrackTik is replaced by a fake `HttpMessageHandler` or a fake client. Warnings and code-style violations fail the build (`TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`).

| Project | Scope |
|---|---|
| `Tests/ManagerEmployee.Domain.Tests` | Aggregate creation, resubmission, success and failure transitions, invariants, field limits, `EmployeeSynced` creation |
| `Tests/ManagerEmployee.Application.Tests` | Sync order (`pending` → TrackTik → `synced`/`failed`), POST vs PUT, retry after failure, event published only after the `synced` commit, queries, report |
| `Tests/ManagerEmployee.Infrastructure.Tests` | Provider 1/2 validation and mapping (every status conversion), EF Core model, read store, conflict translation, TrackTik client (token reuse, refresh after `401`, timeouts, `5xx`, token failures) |
| `Tests/ManagerEmployee.Api.IntegrationTests` | The HTTP pipeline with `WebApplicationFactory` on the EF Core in-memory provider: routes, status codes, ProblemDetails (`400`, `404`, `409`, `415`, `502`, `500`), OpenAPI |
| `Tests/ManagerEmployee.Api.ContainerTests` | The same pipeline against real SQL Server through Testcontainers (`mcr.microsoft.com/mssql/server:2022-latest`): migrations and procedure applied at startup, round trip, resubmission updates one row with a PUT, `failed` persisted on `502`, same email across providers kept apart, concurrent writes become conflicts, status check constraint, report counts |

The container tests need a running Docker engine. One container serves the whole run, and each test gets its own database. If Docker is not reachable, these tests are reported as Inconclusive (skipped) rather than failed.

Run one project, or a single test:

```bash
dotnet test Tests/ManagerEmployee.Api.ContainerTests/ManagerEmployee.Api.ContainerTests.csproj --no-build
dotnet test ManagerEmployee.slnx --no-build --filter "FullyQualifiedName~TestMethodName"
```

## 2. End-to-End with Postman + WireMock

### Why a Stub

The Postman suite tests the real running service: API, EF Core, SQL Server and the real TrackTik HTTP/OAuth client. Only the remote TrackTik server is replaced, by a local [WireMock](https://wiremock.org) container, for three reasons:

- **No credentials.** The stub issues a token to any client, so no personal OAuth credentials are needed.
- **No rate limit.** The hosted mock allows 100 requests per minute and 1000 per hour. The stub has no limit.
- **Failures can be simulated.** The hosted mock never fails, so it cannot exercise the `502`/`failed` path. The stub can.

No application code changes: the Compose override only points `TrackTik__ApiBaseUrl` and `TrackTik__TokenUrl` at the stub.

### How the Stub Behaves

The mappings are in `postman/wiremock/mappings`:

| Mapping | Request | Response |
|---|---|---|
| `token.json` | `POST /oauth/token` | `200` with a fake `access_token`, `expires_in: 3600` |
| `employees-create.json` | `POST /v1/employees` with a Bearer token | `201` |
| `employees-update.json` | `PUT /v1/employees/{id}` with a Bearer token | `200` |
| `employees-fail.json` (takes priority) | POST or PUT whose `email` contains `tracktik-fail` | `500` with the TrackTik error envelope |

The stub records every request it receives (keeping the most recent 2000). The collection queries that log through `POST http://localhost:8089/__admin/requests/count` to check what the API actually sent: POST vs PUT, the Bearer header, and the token request. You can browse it yourself at `http://localhost:8089/__admin/requests`.

### Start the Stack

From the repository root:

```bash
docker compose -p me-postman --env-file postman/postman.env \
  -f docker-compose.yml -f docker-compose.postman.yml up --build --wait
```

This starts SQL Server, the WireMock stub and the API (which applies migrations on startup). `postman/postman.env` contains only local, throwaway values. The stack uses its own project name and ports, so it can run next to the normal Compose stack:

| Service | URL |
|---|---|
| API | `http://localhost:8081` |
| TrackTik stub | `http://localhost:8089` |
| SQL Server | `localhost,14334` |

### Run in the Postman App

1. Import `postman/ManagerEmployee.postman_collection.json` and `postman/ManagerEmployee.local.postman_environment.json`.
2. Select the **ManagerEmployee local (stub)** environment.
3. Open the collection and choose **Run collection**. Keep the default order, since later folders build on earlier ones.

Every request should pass (66 assertions).

### Run with Newman

```bash
cd postman
npm ci          # first time only: installs the pinned Newman and HTML reporter
npm run e2e
```

### What the Collection Covers

| Folder | Checks | Requirements |
|---|---|---|
| 00 Setup | Clears the stub log, creates the run id, OpenAPI lists the `/api/v1` routes | API-001, API-002 |
| 01 Provider1 create | `201` + `Location`, every field mapped, `synced` after 1 attempt; TrackTik got one POST with a Bearer token | FR-001, FR-002, FR-012, FR-016, MAP-001–009, SYNC-005–008, HTTP-009 |
| 02 Provider1 resubmit | `200`, same row updated, `attempts: 2`; TrackTik got a PUT | FR-014, FR-015, FR-017, FR-018, SYNC-012 |
| 03 Get employee | Every stored field is returned | FR-008, FR-009 |
| 04 Provider2 status mapping | `employed`→`active`, `on_leave`→`inactive`, `terminated`→`terminated`, nested fields | FR-003, MAP-010–020 |
| 05 Same email, different provider | Two separate records, never merged | FR-011, FR-013 |
| 06 Validation | Five invalid payloads → `400` keyed by provider field; nothing stored (GET `404`) and nothing sent to TrackTik | FR-005, FR-006, MAP-021–024, SEC-007 |
| 07 Error responses | Unknown provider `404`, missing employee `404`, non-JSON `415`; all `problem+json` with `traceId` | FR-004, FR-010, API-003, API-005 |
| 08 TrackTik failure, then retry | `502` with `syncStatus: failed`, row kept as `failed`; a resubmit with fixed data succeeds, again with POST (it was never created) | FR-007, FR-015, FR-016, SYNC-008–011 |
| 09 Failure after success | A failed update is still sent as PUT, and so is the retry, so the "created in TrackTik" flag survives a failure | FR-017, SYNC-009, SYNC-011 |
| 10 Token handling | At most one token request per run (cached), form-encoded `client_credentials`, no `scope` | HTTP-005, HTTP-006, HTTP-007 |
| 11 Sync status report | Counts are consistent | BONUS-003 |

Every run uses fresh employee IDs (suffixed with a per-run `runId`), so the collection can be rerun against the same stack. On a rerun the token check sees 0 requests, because the API is still using its cached token. The check allows for that.

Not covered over HTTP: the `EmployeeSynced` event has no externally visible effect, so it is covered by the Application tests (published exactly once, only after the `synced` commit).

## 3. Load Testing with Newman

`postman/load/run-load.js` checks that the service handles volume and concurrent traffic without losing or duplicating data.

### What It Does

1. Generates `--count` employees:
   - alternating provider1 and provider2, cycling through all statuses,
   - about **5% TrackTik failures** (the stub returns `500`),
   - about **5% invalid** payloads (bad email).
2. Splits them across `--workers` Newman runs that execute **in parallel**. Each worker runs `postman/load/ManagerEmployee.load.postman_collection.json` once per employee:
   - submit, expecting `201`, `502` or `400`,
   - read back, expecting `200` with the right sync state, or `404` for invalid payloads,
   - resubmit, expecting `200` and `attempts: 2`, `502`, or `400`.

   Every response must also come back within `--max-ms`.
3. Reads `GET /api/v1/reports/sync-status` before and after the run and checks that the database changed by exactly what the data should produce:
   - synced rows added = valid employees,
   - failed rows added = TrackTik failures,
   - pending rows added = 0 (nothing stuck mid-sync),
   - total rows added = valid + failures (no duplicates, invalid payloads not stored).

### Run It

Start the stack as in [Start the Stack](#start-the-stack), then:

```bash
cd postman
npm ci                                         # first time only
npm run load -- --count 2000 --workers 8
```

| Option | Default | Meaning |
|---|---|---|
| `--count` | `1000` | Number of employees to generate (each costs 3 API requests) |
| `--workers` | `4` | Parallel Newman runs |
| `--max-ms` | `2000` | Maximum allowed response time per request |
| `--base-url` | `http://localhost:8081` | API under test |

### Reading the Result

The console shows:

- a **Workers** table: requests, assertions, failures, and average and maximum response time per worker,
- a **Database totals** table: each before/after check with its actual value, expected value, and pass/fail,
- the overall throughput, then `PASSED` or `FAILED`.

The command exits with code 1 if any assertion or database check fails, so it can gate a CI job.

Reports are written to `postman/load/out/<runId>/` (ignored by Git):

| File | Content |
|---|---|
| `report-wN.html` | HTML report per worker (htmlextra), listing only failures |
| `junit-wN.xml` | JUnit results per worker, for CI |
| `summary.json` | Parameters, before/after report, database checks, per-worker statistics, overall result |

Latest verified run: 2,000 employees, 8 workers, 6,000 requests in about 52 s (about 115 requests/s). Average response time was about 25 ms and the slowest 201 ms. There were 0 failed assertions, and all four database checks passed.

### Limits

- TrackTik is the stub, so this measures the API and SQL Server, not the real TrackTik.
- Never point a load run at the hosted TrackTik mock. It allows 100 requests per minute and would be rate-limited.
- The stub returns immediately. With a real TrackTik, each submission also waits for the remote call, which has a 10-second timeout.

## 4. Teardown and Troubleshooting

Stop the Postman stack:

```bash
docker compose -p me-postman --env-file postman/postman.env \
  -f docker-compose.yml -f docker-compose.postman.yml down -v
```

`down -v` also deletes the database volume. Use `down` without `-v` to keep the data between runs.

| Problem | Fix |
|---|---|
| Port 8081, 8089 or 14334 already in use | Change `API_PORT`, `STUB_PORT` or `SQLSERVER_PORT` in `postman/postman.env`. If you change the API or stub port, also update the Postman environment, or pass `--base-url` to the load runner. |
| `newman` not found / `Cannot find module 'newman'` | Run `npm ci` inside `postman/` |
| The report endpoint fails at the start of a load run | The stack is not running. Start it and check it with `docker compose -p me-postman ps` |
| A stub check fails with unexpected counts | Run the whole collection from `00 Setup`, which clears the stub log, and don't run load and end-to-end at the same time |

To look at the data in SSMS (or any SQL client), connect with SQL Server Authentication:

| Stack | Server | Login | Password |
|---|---|---|---|
| Postman stack | `localhost,14334` | `sa` | `SQLSERVER_SA_PASSWORD` from `postman/postman.env` |
| Normal Compose stack | `localhost,1433` | `sa` | `SQLSERVER_SA_PASSWORD` from your `.env` |

Check **Trust server certificate** and open the `ManagerEmployee` database. Employees are in `dbo.Employees`, and the report procedure is `dbo.usp_GetEmployeeSyncStatusCounts`.

## 5. Continuous Integration

[`.github/workflows/ci.yml`](../.github/workflows/ci.yml) runs on every push to `main`, on every pull request, and on demand (**Actions → CI → Run workflow**). Both jobs use `ubuntu-latest`, because the SQL Server image is Linux x64 only, and they need no GitHub secrets.

| Job | Steps | Artifact |
|---|---|---|
| **Build, test and format** | `dotnet restore` → `build` → `test` (all five projects, with the Testcontainers SQL Server tests using the runner's Docker) → test summary → `dotnet format --verify-no-changes` | `test-results` (TRX files) |
| **Postman E2E and load** (runs after the first job passes) | Start the Postman stack (API + SQL Server + WireMock) → `npm run e2e` → `npm run load -- --count 500 --workers 4` → stop the stack | `load-reports` (HTML, JUnit, `summary.json`) |

The test summary step (`.github/scripts/summarize-trx.py`) adds a table of passed, failed and skipped tests per project to the run page. It fails the job if the SQL Server container tests were skipped. Those tests report Inconclusive when Docker is unavailable, so without this check CI could pass without ever touching SQL Server. When the E2E job fails, it prints the SQL Server, API and stub container logs.

To reproduce a CI failure locally, run the same commands: the sequence in [Automated tests](#1-automated-tests), then the stack and `npm` commands in sections 2 and 3.
