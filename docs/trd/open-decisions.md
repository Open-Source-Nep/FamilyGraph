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
- Project references and persistence abstractions (D-002 below).
- **First implementation slice (agreed 2026-10-21):** domain core only, in `FamilyGraph.Business`. It covers people, maintainers, parent–child links, per-endpoint consent and audit events, with unit tests. It uses no persistence, identity or HTTP, so it is not blocked by the decisions below.
- **Unit-test framework (agreed 2026-10-21):** xUnit v3. The integration-test database strategy is still open (see Testing below).
- Assertions use xUnit's built-in `Assert` (D-003 below).
- Test project layout (D-004 below).
- Mapping PRD acceptance criteria to tests (D-005 below).
- Client end-to-end smoke tests with Playwright (D-006 below).
- Domain rule enforcement and error reporting (D-007 below).
- Business contains no DTOs or API contracts. Account and user DTOs wait for the identity and API-convention decisions.

## Proposed, not yet finalized

- PostgreSQL as the relational database.
- EF Core for persistence and migrations.
- Microsoft `dotnet new angular` template as the starting point, subject to checking its current structure and compatibility.

## Decisions to discuss next

1. **Identity approach:** Decide supported sign-in methods, identity provider strategy, account lifecycle, and whether ASP.NET Core Identity is the chosen foundation.
2. **Database and graph queries:** Confirm PostgreSQL and define initial traversal/query patterns, limits, and indexing approach. Settle the integration-test database strategy alongside it (see item 4).
3. **API conventions:** Decide endpoint style, DTO placement, validation, pagination, and error format.
4. **Testing (#7):** xUnit v3, built-in `Assert` (D-003), the test project layout (D-004), acceptance-criteria mapping (D-005) and Playwright e2e smoke tests (D-006) are agreed. Deferred until the database decision (item 2, #5): the integration-test database strategy. Testcontainers is the leading option if PostgreSQL is confirmed; in-memory and SQLite providers were rejected because they do not reproduce PostgreSQL constraints, transactions or recursive queries.
5. **Production hosting:** Decide whether Client assets are served by Server or hosted separately, while keeping local development on the SPA Proxy.
6. **CI/CD and configuration:** Decide build pipeline, migration deployment policy, secrets management, and supported deployment configuration.

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

### D-002: Project references and persistence abstractions

- **Status:** agreed (2026-10-21). Issue #3.
- **Context:** The TRD draft allowed persistence interfaces in either Business or Application, and an optional Infrastructure → Application reference. Graph reads must be bounded, and the consent and derivation rules must be testable in isolation.
- **Options considered:**
  - Infrastructure → Business only (all interfaces in Business), or Infrastructure → Application + Business.
  - Mixed interface ownership, or Application-only ownership.
  - EF Core used directly in Application, or focused store interfaces.
  - Graph reads through domain objects, or dedicated query services.
  - Derivation done in SQL, or in a Business service.
- **Trade-offs:** Mixed ownership needs case-by-case judgement and drifts. EF Core in Application leaks persistence concerns. Walking domain objects in memory is hard to keep bounded at 10k+ people. Derivation in SQL puts product rules where they are hard to test.
- **Decision:**
  - References: Server → Application, Infrastructure; Application → Business; Infrastructure → Business, Application; Business → nothing.
  - Application owns all persistence and query interfaces. Business has none.
  - Write side: focused per-aggregate store interfaces plus `IUnitOfWork`. Audit events are saved in the same transaction.
  - Read side: Application-owned query services that return read models, with depth and node limits as required parameters.
  - Derived relationships: a pure Business service computes them from a bounded set of active links fetched by a query service.
- **Consequences:** Wiring and the architecture test are done in #8. Limit values depend on the database decision (#5). The audit-in-same-transaction rule shapes #10.

### D-003: Assertion library

- **Status:** agreed (2026-10-21). Issue #7.
- **Context:** Every test uses the assertion library, so switching later means rewriting tests. Consent and audit tests mostly compare states and event sequences. New dependencies need agreement.
- **Options considered:** xUnit built-in `Assert`; Shouldly; AwesomeAssertions. FluentAssertions v8+ was excluded because of its commercial licence.
- **Trade-offs:** `Assert` adds no dependency, but its object-graph comparison is weaker. Shouldly gives clearer messages for one small dependency. AwesomeAssertions is the most expressive, but has the largest API and dependency.
- **Decision:** Use xUnit's built-in `Assert`. Add no assertion library.
- **Consequences:** Prefer records and value equality in Business so that `Assert.Equal` is enough. If messages prove inadequate, moving to Shouldly is a mechanical change that needs a new agreement.

### D-004: Test project layout

- **Status:** agreed (2026-10-21). Issue #7.
- **Context:** #8 creates the test projects. The integration-test database strategy is still open.
- **Options considered:** Business and architecture tests only, adding the others later; one test project per production project; a single test project.
- **Trade-offs:** Creating projects only when needed means less scaffolding up front. One project per production project keeps a clear home for tests and mirrors the reference graph, at the cost of some near-empty projects at first.
- **Decision:** Under `tests/`:
  - `FamilyGraph.Business.Tests`, `FamilyGraph.Application.Tests` and `FamilyGraph.Infrastructure.Tests`: unit tests, each referencing only its project under test.
  - `Server.IntegrationTests`: HTTP-level tests that host `Server` in-process.
  - `FamilyGraph.ArchitectureTests`: enforces the D-002 reference graph (#8).
- **Consequences:** Unit tests do not touch a real database. Tests that need one wait for the integration-test database decision. Any test host package (e.g. `Microsoft.AspNetCore.Mvc.Testing`) is introduced in #8 with explicit mention.

### D-005: Mapping PRD acceptance criteria to tests

- **Status:** agreed (2026-10-21). Issue #7.
- **Context:** PRD acceptance criteria were unnumbered bullets, so tests could not reference them and coverage gaps were invisible.
- **Options considered:** stable criterion IDs with test traits; descriptive test names only; a coverage table maintained in the TRD.
- **Trade-offs:** IDs require a one-time PRD edit and discipline when adding criteria. Names alone carry no maintenance cost but give no mechanical traceability. A hand-maintained table drifts.
- **Decision:**
  - Every PRD acceptance criterion has a stable ID, `AC-<PRD>.<n>`. The rules for keeping IDs stable are in `docs/prd/README.md`.
  - A test that verifies a criterion carries `[Trait("AC", "AC-03.1")]`, with one trait per criterion it covers.
  - Test names still describe the behaviour, e.g. `Disagreement_deactivates_link_immediately`.
- **Consequences:** `dotnet test --filter "AC=AC-03.1"` selects a criterion's tests. Tests for TRD-level rules (e.g. architecture tests) carry no `AC` trait.

### D-006: Client end-to-end tests

- **Status:** agreed (2026-10-21). Issue #7.
- **Context:** Server-side integration tests cover privacy, consent and permission rules. Browser tests mainly guard UI wiring and key user flows. Vitest already covers component tests.
- **Options considered:** no browser e2e for the first release; Playwright smoke tests; defer the decision.
- **Trade-offs:** Playwright adds an npm dependency, browser binaries in CI, and run time. Its tests need a running Server and database, so they depend on the hosting and database decisions. Skipping e2e leaves UI wiring regressions to manual checks.
- **Decision:** Use Playwright (`@playwright/test`) for a small set of smoke tests over key user flows in the first release. Keep them in `src/client/e2e/`, sharing the client's `package.json`. Component and logic tests stay in Vitest.
- **Consequences:** The dependency and configuration are added with the first UI flow worth covering, not before. The CI decision must provide browsers plus a running Server and database. Smoke tests follow D-005 using Playwright tags, e.g. `test('shows a local subgraph view', { tag: '@AC-08.4' }, ...)`.

### D-007: Domain rule enforcement and error reporting

- **Status:** agreed (2026-10-21). Issue #12.
- **Context:** The domain core (#12, #14, #15, #16) must reject invalid operations, such as an empty first name, a rename by someone other than the maintainer, or a link that would create a cycle. Callers need to know which rule failed and why.
- **Options considered:** a result type returned from domain operations; custom exceptions; checking permissions only in Application.
- **Trade-offs:** A result type makes failures explicit in signatures but adds plumbing to every call. Exceptions keep domain signatures simple and stop execution at the violated rule, at the cost of being invisible in signatures. Permission checks only in Application would let a code path bypass the rules.
- **Decision:**
  - Domain entities enforce their own invariants and authority rules. For example, `Person` refuses a rename from anyone other than its maintainer.
  - Rule violations throw custom exceptions with a clear error message. Business defines a base domain exception plus specific subclasses per rule. Application defines its own exceptions for application-level failures (e.g. not found). Neither depends on HTTP types.
  - Exceptions are for rule violations, not normal control flow. Domain code does not catch and swallow them.
- **Consequences:** Unit tests assert the exception type thrown for each rule. Mapping exceptions to HTTP responses belongs to the API-conventions decision (#6). Message localization is not decided.
