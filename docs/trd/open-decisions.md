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
- Repository layout, toolchain versions, and styling approach (D-001 below).

## Proposed, not yet finalized

- PostgreSQL as the relational database.
- EF Core for persistence and migrations.
- Microsoft `dotnet new angular` template as the starting point, subject to checking its current structure and compatibility.
- Repository/query abstraction ownership split between Business and Application based on responsibility.

## Decisions to discuss next

1. **Project references and persistence abstractions:** Confirm whether the proposed reference graph is acceptable and when repository/query interfaces belong to Business versus Application.
2. **Identity approach:** Decide supported sign-in methods, identity provider strategy, account lifecycle, and whether ASP.NET Core Identity is the chosen foundation.
3. **Database and graph queries:** Confirm PostgreSQL and define initial traversal/query patterns, limits, and indexing approach.
4. **API conventions:** Decide endpoint style, DTO placement, validation, pagination, and error format.
5. **Testing:** Decide test frameworks and integration-test database strategy.
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

## Decision records

### D-001: Reconcile template scaffold with TRD

- **Status:** agreed (2026-10-21). Issue #2.
- **Context:** The generated template differed from the proposed TRD layout: `.slnx` instead of `.sln`, projects under `src/`, Angular folder `src/client`, Angular 21, and Tailwind CSS 4 included.
- **Options considered:** follow the TRD as written (move folders, regenerate `.sln`), or keep the template layout and update the TRD; upgrade Angular now or later; keep or remove Tailwind; pin toolchains now or defer to CI.
- **Trade-offs:** Moving folders gives no functional benefit and adds churn. `.slnx` needs recent tooling (.NET 9+ SDK, VS 2022 17.13+, Rider). Upgrading before feature code exists is the cheapest time to do it. Tailwind is a utility library rather than a component library, but it still adds long-term dependencies and conventions.
- **Decision:**
  - Keep `FamilyGraph.slnx` and all projects under `src/`.
  - Keep the Angular folder as `src/client` (`client.esproj`). "Client" remains the logical project name.
  - Upgrade to Angular 22 (TypeScript 6.0) and use the v22 defaults. The `ng update` compatibility shims (eager change detection, suppressed nullish diagnostics) were not kept.
  - Remove Tailwind CSS (and the PostCSS config). Use plain component-scoped CSS and CSS custom properties.
  - Pin toolchains: `global.json` (.NET SDK 10.0.1xx, `latestPatch`), `.nvmrc` (Node 24), and `engines` in `package.json`.
- **Consequences:** Contributors need .NET SDK 10.0.1xx and Node 22.22.3+ or 24.15+. Any later styling dependency needs a separate agreement.
