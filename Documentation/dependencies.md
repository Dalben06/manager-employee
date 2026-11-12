# Dependency Inventory

All direct NuGet package versions are managed in `Directory.Packages.props`. The versions below were selected as current stable releases compatible with .NET 10 when the scaffold was created.

| Package | Version | Projects | Concrete need | Why the platform alone is insufficient |
|---|---:|---|---|---|
| `Microsoft.AspNetCore.OpenApi` | 10.0.15 | `ManagerEmployee.Api` | Generate and serve the OpenAPI document through `AddOpenApi` and `MapOpenApi`. | ASP.NET Core supplies the hosting primitives, but the OpenAPI document services are distributed through this package. |
| `Microsoft.EntityFrameworkCore.SqlServer` | 10.0.15 | `ManagerEmployee.Infrastructure` | Provide EF Core SQL Server persistence, migrations, composite identity, and optimistic concurrency. | EF Core provider support is not part of the ASP.NET Core shared framework. |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.15 | `ManagerEmployee.Infrastructure` | Enable design-time migration discovery and generation. It is marked `PrivateAssets=all`. | Migration tooling requires design-time services that should not flow to consumers. |
| `Microsoft.EntityFrameworkCore.InMemory` | 10.0.15 | `ManagerEmployee.Infrastructure.Tests` | Verify no-tracking read-store projections and composite-identity behavior without requiring a shared SQL Server during the default test run. | The SQL Server provider cannot execute portable isolated tests without an external database; migration application remains the authority for SQL Server behavior. |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.0.15 | `ManagerEmployee.Api.IntegrationTests` | Host the real API with `WebApplicationFactory` for integration tests. | The shared framework does not include the test-host package. |
| `MSTest` | 4.5.2 | All four test projects | Supply the MSTest framework, adapter, analyzers, and test SDK through its meta-package. | .NET does not include a test framework or runner adapter by default. |

FluentValidation, FluentAssertions, and Moq are not included. Domain values validate and normalize themselves through focused factories. MediatR, AutoMapper, JSON-schema libraries, resilience packages, NATS clients, and Testcontainers remain intentionally excluded until concrete mandatory behavior requires them.
