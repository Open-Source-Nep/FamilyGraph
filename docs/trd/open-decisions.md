# Open Technical Decisions

Use this file to track unresolved technical choices. Do not treat recommendations as final until discussed and agreed.

## Agreed so far

- Product is an open-source, self-hostable community family graph.
- Backend: ASP.NET Core / .NET; frontend: Angular SPA.
- Use ASP.NET Core Angular SPA Proxy for local development integration.
- Solution name is `FamilyGraph`.
- API project is named `Server`; Angular project is named `Client`.
- Class libraries are `FamilyGraph.Business`, `FamilyGraph.Application`, and `FamilyGraph.Infrastructure`.
- Business owns domain rules and domain services.
- Application handles user management, authentication/authorization workflows, permissions, and use-case orchestration.
- Infrastructure contains EF Core `DbContext`, migrations, persistence, and may use `IdentityDbContext`.
- Keep a modular monolith initially and avoid unnecessary abstraction.
- Do not write application code until the technical specification is sufficiently agreed.

- **First implementation slice (agreed 2026-10-21):** domain core only, in `FamilyGraph.Business`. It covers people, maintainers, parent–child links, per-endpoint consent and audit events, with unit tests. It uses no persistence, identity or HTTP, so it is not blocked by the decisions below.
- **Unit-test framework (agreed 2026-10-21):** xUnit v3. The integration-test database strategy is still open (see Testing below).
- Business contains no DTOs or API contracts. Account and user DTOs wait for the identity and API-convention decisions.

## Proposed, not yet finalized

- .NET 10 LTS and Angular 22.
- PostgreSQL as the relational database.
- EF Core for persistence and migrations.
- Microsoft `dotnet new angular` template as the starting point, subject to checking its current structure and compatibility.
- One repository containing Server, Client, class libraries, tests, docs, and deployment files.
- Repository/query abstraction ownership split between Business and Application based on responsibility.

## Decisions to discuss next

1. **Project references and persistence abstractions:** Confirm whether the proposed reference graph is acceptable and when repository/query interfaces belong to Business versus Application.
2. **Identity approach:** Decide supported sign-in methods, identity provider strategy, account lifecycle, and whether ASP.NET Core Identity is the chosen foundation.
3. **Database and graph queries:** Confirm PostgreSQL and define initial traversal/query patterns, limits, and indexing approach.
4. **API conventions:** Decide endpoint style, DTO placement, validation, pagination, and error format.
5. **Testing:** xUnit v3 is agreed for unit tests. Still to decide: the integration-test database strategy.
6. **Production hosting:** Decide whether Client assets are served by Server or hosted separately, while keeping local development on the SPA Proxy.
7. **CI/CD and configuration:** Decide build pipeline, migration deployment policy, secrets management, and supported deployment configuration.

## Decision-record convention

For each major decision, record:
- Decision and status (proposed / agreed / superseded).
- Context and constraints.
- Options considered.
- Trade-offs.
- Chosen option and rationale.
- Consequences and follow-up work.
