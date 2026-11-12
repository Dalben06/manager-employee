# ADR 0001: Use a Hybrid Layered Solution With Controllers

- Status: Accepted
- Date: 2026-11-11

## Context

The assessment requires a small ASP.NET Core service with provider-specific validation and mapping, SQL Server persistence, TrackTik synchronization, OAuth token handling, events, and meaningful tests. The structure must make these responsibilities independently understandable and testable without becoming a framework demonstration.

## Decision

Use six production projects:

- `ManagerEmployee.Domain` for core concepts, validation, invariants, and aggregate repository abstractions.
- `ManagerEmployee.Application` for use cases, orchestration, and external-system/event ports.
- `ManagerEmployee.Infrastructure` for EF Core, HTTP, OAuth, and event-publisher adapters.
- `ManagerEmployee.Infrastructure.Provider1.Integration` for Provider 1 contracts, validation, and mapping.
- `ManagerEmployee.Infrastructure.Provider2.Integration` for Provider 2 contracts, validation, and mapping.
- `ManagerEmployee.Api` for controllers, HTTP contracts, middleware, OpenAPI, and dependency composition.

Keep each production project directly under `Source/` and each test project directly under `Tests/`. Use `Apps` and `Services` only as logical solution folders in `ManagerEmployee.slnx`; do not mirror those categories as physical directories.

Use feature-oriented folders inside projects once behavior exists. Use ASP.NET Core controllers for the HTTP transport. Mirror production responsibilities with Application, Infrastructure, and API integration test projects; do not create a Domain test project until the Domain contains behavior worth testing.

## Alternatives Considered

### Single ASP.NET Core Project

This would minimize project count but would make provider mapping, synchronization policy, persistence, and HTTP concerns easier to couple accidentally. It was rejected because independent tests and dependency direction are important evaluation criteria.

### Strict Clean Architecture Template

A template with separate contracts, use-case, adapters, shared kernel, mediator, and presentation projects could enforce more boundaries. It was rejected because the mandatory service is small and those layers would add navigation and abstraction costs without proven behavior.

### Minimal APIs

Minimal APIs would reduce transport ceremony. Controllers were selected because they make HTTP contracts, conventions, versioned routes, and `ProblemDetails` handling more explicit for reviewers. The user confirmed this choice.

## Consequences

- Dependency direction is visible in project references and easy to review.
- The flat physical layout keeps project paths short, while solution folders preserve the application/service distinction in IDEs.
- Application orchestration and infrastructure adapters can be tested independently.
- The API may reference Infrastructure as the composition root, but business behavior must not move into controllers.
- Ten projects create some initial ceremony, offset by isolated provider contracts, clear ownership, and a single solution-level test command.
- New interfaces and patterns will be introduced only when concrete behavior needs them.

## Explicit Exclusions

MediatR, a custom dispatcher, AutoMapper, a generic repository, a custom framework, and an event-bus framework are not part of the mandatory architecture. Command-style use-case names may be used later without claiming the bonus CQRS requirement.
