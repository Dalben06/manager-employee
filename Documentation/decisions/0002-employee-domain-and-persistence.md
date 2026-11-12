# ADR 0002: Use One Normalized Employee Aggregate and EF Core Repository

- Status: Accepted
- Date: 2026-11-11

## Context

The service accepts two provider-specific representations of an employee but must store one local record, synchronize a normalized TrackTik representation, update resubmissions, and retain enough local state to choose TrackTik POST or PUT. The persistence model must enforce the composite provider identity and the supported employment and synchronization statuses.

The [ABCCompany](https://github.com/Dalben06/AbcCompany) and [Equinox](https://github.com/EduardoPires/EquinoxProject) examples place aggregate repository abstractions and validation with domain behavior. Their Dapper connection factories, HTTP response wrappers, MediatR messages, framework base entities, and validation framework are not needed here, but their focused repository and validation placement fits the selected DDD boundary.

## Decision

Use one `Employee` domain aggregate and one `Employees` table. Provider payloads are validated and mapped before they reach this normalized model; raw provider JSON and separate provider tables are not persisted.

The aggregate stores:

- The immutable identity `(Provider, ProviderEmployeeId)`.
- Normalized TrackTik employee fields.
- `SyncStatus`, `Attempts`, `LastAttemptAt`, and `LastError`.
- `HasBeenCreatedInTrackTik`, which remains true after later failures and is the durable source for choosing PUT instead of POST.

The TrackTik employee identifier is derived as `{provider}-{providerEmployeeId}` at the Application synchronization boundary and is not stored as a separate mutable column or exposed by the aggregate. `StartDate` uses `DateOnly?` in .NET and SQL Server `date` in persistence.

The aggregate accepts cohesive `EmployeeIdentity` and `EmployeeSynchronizationAttempt` objects instead of long primitive parameter lists. `EmployeeIdentity`, `EmployeeProfile`, `EmployeeSynchronizationAttempt`, and `SynchronizationError` use private constructors and focused factories so invalid or unnormalized domain values cannot be passed to aggregate transitions. Shared field limits live in `EmployeeFieldLimits` rather than on the aggregate. Provider request validation and exact JSON date parsing remain provider-boundary concerns because those adapters must collect field-level errors rather than throw domain construction exceptions.

Use `(Provider, ProviderEmployeeId)` as the SQL Server composite primary key, matching the contractual domain identity. Store provider, employment status, and synchronization status as lowercase contract strings protected by SQL check constraints. A SQL Server `rowversion` shadow property provides optimistic concurrency detection without adding an EF-specific property to the domain type. ADR 0003 records the replacement of the earlier technical `long` key decision.

Keep the aggregate-specific `IEmployeeRepository` abstraction in `ManagerEmployee.Domain`, matching the repository placement used by the [ABCCompany](https://github.com/Dalben06/AbcCompany) and [Equinox](https://github.com/EduardoPires/EquinoxProject) examples. Keep its EF Core implementation, `DbContext`, fluent entity configuration, and migrations in `ManagerEmployee.Infrastructure`. Application consumes the abstraction to make the required pending and outcome commits explicitly. Do not introduce a generic repository or a separate `ManagerEmployee.Data` project.

Configuration binding remains in the API composition root and Infrastructure registration. HTTP response representations remain in the API. Neither environment settings nor `ProblemDetails`/HTTP response wrappers belong in Domain.

## Concurrency Policy

The composite primary key is the final guarantee against duplicate records when first submissions race. Row-version concurrency prevents silent last-write-wins updates to an already stored employee. A bounded process-local coordinator serializes equal identities in the single-instance deployment, and Infrastructure translates remaining unique-key and optimistic-concurrency failures to an explicit commit conflict. No database transaction is held open while calling TrackTik.

## Alternatives Considered

### Separate Provider Tables

Rejected because both payloads map to the same TrackTik model and local identity already includes the provider. Separate tables would duplicate synchronization state and repository behavior.

### Persist Raw Provider JSON

Rejected because retrieval and synchronization require normalized fields, and no auditing or replay requirement justifies storing unvalidated transport documents.

### Store the Derived TrackTik Identifier

Rejected because it is deterministic from immutable identity fields. Duplicating it would create an avoidable consistency invariant.

### Add `ManagerEmployee.Data`

Rejected because ADR 0001 already assigns database adapters to Infrastructure. A second data-access project would add another boundary without a separate responsibility.

### Copy the Example Core Library

Rejected because its Dapper, database-strategy, HTTP response, MediatR, and configuration types are unrelated to this service's domain and would violate the lightweight EF Core architecture.

## Consequences

- Provider records sharing an email remain independent.
- A failed retry after an earlier success still uses TrackTik PUT.
- Domain state can be tested without EF Core or ASP.NET Core.
- Domain validation and normalization are expressed through self-validating values and execute before aggregate mutation.
- Domain field limits are centralized outside the aggregate and shared by validation and persistence configuration.
- Application depends on the Domain repository abstraction, while Infrastructure supplies its EF Core implementation.
- EF Core maps the domain aggregate directly while all persistence details remain in Infrastructure configuration.
- Simultaneous same-identity submissions are serialized within one process, and cross-instance races cannot create duplicate rows because the composite identity is the primary key.
