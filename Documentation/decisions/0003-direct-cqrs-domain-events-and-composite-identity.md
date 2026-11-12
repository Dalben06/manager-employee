# ADR 0003: Use Direct CQRS, Post-Commit Domain Events, and Composite Employee Identity

- Status: Accepted
- Date: 2026-11-11

## Context

The service has one synchronization write use case and one employee retrieval read use case. The challenge rewards clear command/query separation but explicitly favors a lightweight design. Employee identity is contractually `(provider, providerEmployeeId)`, and `EmployeeSynced` must be published only after TrackTik succeeds and the final local commit succeeds.

The original persistence design used a technical `long` key and exposed field limits and a derived TrackTik identifier from the aggregate. Those details obscured the aggregate's actual identity and behavior. The [ABCCompany](https://github.com/Dalben06/AbcCompany) and [Equinox](https://github.com/EduardoPires/EquinoxProject) examples demonstrate aggregate repositories, explicit commands, read models, and domain events, but their mediator frameworks, base entities, generic units of work, and event-sourcing infrastructure are unnecessary here.

## Decision

- Use directly injected `SyncEmployeeCommandHandler` and `GetEmployeeQueryHandler` classes. Do not add MediatR, a custom dispatcher, marker interfaces, or pipeline behaviors.
- Keep writes aggregate-oriented through the Domain-owned `IEmployeeRepository`.
- Keep reads projection-oriented through an Application-owned `IEmployeeReadStore` implemented with EF Core `AsNoTracking`.
- Return immutable Application models from commands and queries; never expose an EF-tracked aggregate to transport code.
- Use `(Provider, ProviderEmployeeId)` as the EF Core primary key and remove the technical domain `Id`.
- Keep shared field limits in `EmployeeFieldLimits`, not on `Employee`.
- Derive TrackTik's identifier at the Application synchronization boundary rather than exposing it from the aggregate.
- Have `Employee.MarkSynced` return one typed `EmployeeSynced` domain event. Application publishes that event only after the `synced` commit succeeds.
- Serialize same-identity synchronization inside one service process with a bounded striped asynchronous coordinator. Retain the composite key and `rowversion` as database safeguards.
- Represent expected commit conflicts with `EmployeeCommitOutcome`; do not expose EF Core or SQL Server exception types to Application.
- Never hold a database transaction or lock while calling TrackTik.

## Consequences

- Command and query behavior is explicit without framework indirection.
- Domain identity matches the challenge contract and database key.
- Query execution does not track aggregates or risk accidental writes.
- Event timing remains visible in the command handler and independently testable.
- Process-local coordination prevents duplicate simultaneous work in the provided single-instance deployment.
- A multi-instance deployment can still encounter an optimistic persistence conflict; handling distributed serialization would require a distributed lock or queue and is outside this assessment.
- Composite foreign keys would be more verbose if relationships are introduced later, but the current model has no dependent tables.

## Rejected Alternatives

### Mediator or Dispatcher

Rejected because two use cases do not justify hidden dispatch, registration conventions, or pipeline abstractions.

### Generic Repository and Unit of Work

Rejected because synchronization requires explicit employee-specific lookup and two visible commit boundaries.

### Pre-Commit Event Dispatch

Rejected because it would violate the requirement that `EmployeeSynced` is published only after the `synced` state is committed.

### Unbounded Per-Identity Lock Dictionary

Rejected because it can retain one semaphore for every identity indefinitely. A fixed stripe set provides bounded memory while still serializing equal identities.
