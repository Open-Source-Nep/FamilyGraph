# Architecture

## Status

Agreed. Detailed choices are recorded as decisions in `open-decisions.md`; none are open.

## Product context

FamilyGraph is an open-source, self-hostable, community-maintained family graph. The product prioritizes privacy, relationship consent, reliable relationship history, and graph exploration.

Product behavior is defined by the separate PRDs. Technical design must preserve their rules, especially around independent profile claims, relationship consent, privacy, disagreement, and audit history.

## Architectural baseline

- Use a modular monolith initially; do not introduce microservices without a demonstrated need.
- Use an ASP.NET Core backend and an Angular SPA.
- Integrate the Angular development server with ASP.NET Core using the Angular SPA Proxy during development.
- Do not treat the development proxy as the production deployment strategy.
- Keep domain rules independent of HTTP, Angular, EF Core, and identity-provider implementation details where practical.
- Start with one relational database unless requirements or measured performance justify another store.
- Graph exploration must be bounded and must not require loading or rendering the entire graph.
- Privacy and consent must be enforced server-side, including in graph traversal and derived relationship queries.

## Agreed backend responsibilities

- `Server`: ASP.NET Core HTTP entry point, middleware, endpoint mapping, dependency-injection composition, and API configuration.
- `FamilyGraph.Application`: application use cases, user-management workflows, authentication/authorization workflows, permissions, and orchestration.
- `FamilyGraph.Business`: domain models, domain rules, and domain services.
- `FamilyGraph.Infrastructure`: EF Core persistence, `DbContext`, migrations, database mappings, and infrastructure implementations.
- `Client`: Angular SPA.

Infrastructure may use an EF Core context derived from `IdentityDbContext` where appropriate. User management, permission workflows, and application-level decisions remain in Application; Infrastructure provides persistence and technical implementations.

## Architectural principles

- Prefer clear boundaries over unnecessary abstractions.
- Do not add libraries or infrastructure without explaining the value and trade-offs.
- Do not introduce a separate graph database, message broker, or distributed architecture by default.
- Do not make derived relationships authoritative facts unless a clear technical reason is documented.
- Preserve audit/history requirements and apply consent rules consistently across all read and write paths.
