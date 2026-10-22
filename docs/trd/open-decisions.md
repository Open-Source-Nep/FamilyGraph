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
- Identity approach and account model (D-008 below).
- Database, integration-test database and bounded graph queries (D-009 below).
- API conventions (D-010 below).
- Production hosting and deployment (D-011 below).
- CI/CD, configuration and releases (D-012 below).
- Business contains no DTOs or API contracts (D-010 places API models in Server).
- **Domain enumerations (agreed 2026-10-22, PR #44 review):** closed sets of domain values are sealed classes deriving from `FamilyGraph.Business.BaseEnum<T>`, with one `public static readonly` field per value and a stable integer `Value` for persistence. C# `enum` is not used in the domain.
- Consent state transitions (D-013 below).
- Link removal (D-014 below).
- **Name length (agreed 2026-10-21):** Business puts no length cap on name parts. Any storage or input limit is set later by persistence (#9) and API validation (D-010).

## Decisions to discuss next

1. **Identity approach:** agreed (D-008). Sessions use cookies (D-011).
2. **Database and graph queries:** agreed (D-009).
3. **API conventions:** agreed (D-010).
4. **Testing (#7):** xUnit v3, built-in `Assert` (D-003), the test project layout (D-004), acceptance-criteria mapping (D-005) and Playwright e2e smoke tests (D-006) are agreed. The integration-test database is Testcontainers with PostgreSQL (D-009).
5. **Production hosting:** agreed (D-011).
6. **CI/CD and configuration:** agreed (D-012).

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
  - Rule violations throw custom exceptions with a clear error message and a stable error code (e.g. `link.cycle_detected`, see D-010). Business defines a base domain exception plus specific subclasses per rule. Application defines its own exceptions for application-level failures (e.g. not found). Neither depends on HTTP types.
  - Exceptions are for rule violations, not normal control flow. Domain code does not catch and swallow them.
- **Consequences:** Unit tests assert the exception type thrown for each rule. Mapping exceptions to HTTP responses belongs to the API-conventions decision (#6). Message localization is not decided.

### D-008: Identity approach and account model

- **Status:** agreed (2026-10-21). Issue #4.
- **Context:** `UserAccount` must stay separate from `Person` (PRD 01). Admins are needed for claim review (PRD 04) and investigations (PRD 07). The product is self-hostable, so setup cost matters.
- **Options considered:** local passwords, external OIDC, or both; ASP.NET Core Identity or an external identity server (e.g. Keycloak); shared or separate `DbContext`; first-sign-in admin, admin granted in the database, or admin named in configuration.
- **Trade-offs:** Google-only sign-in stores no passwords and is quick to build, but every instance needs its own Google OAuth client and users need a Google account. An external identity server is one more service for every self-hoster. Separate contexts would need cross-context transactions for writes that touch accounts and audit records.
- **Decision:**
  - **Sign-in:** Google only for the first release. Email/password, usernames and phone numbers may be added later.
  - **Foundation:** ASP.NET Core Identity, with its types kept in Infrastructure. Business never sees them; the domain refers to accounts by an opaque account ID.
  - **Database:** one shared database and one EF Core context derived from `IdentityDbContext`, so domain writes, account data and audit events can commit in one transaction.
  - **Roles:** `User` and `Administrator`. Every new account gets `User`. "Maintainer" is not a role; it is tracked per profile in the domain.
  - **Admin assignment:** configuration holds a list of admin email addresses (e.g. `Admin:Emails`). On each sign-in, an account whose Google-verified email is on the list gets `Administrator`. An account not on the list loses it. Unverified emails never match. There is no in-app promote or demote.
  - **First-release account scope:** sign up and sign in with Google, sign out, view own account. Account deletion waits for #32.
- **Consequences:**
  - New NuGet packages (Microsoft, first-party): `Microsoft.AspNetCore.Authentication.Google` and `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, added with #11.
  - Self-host docs must explain creating a Google OAuth client. Secrets handling belongs to #34.
  - Cookie or token sessions for the SPA are decided with API conventions (#6) and hosting (#33).

### D-009: Database, integration-test database and bounded graph queries

- **Status:** agreed (2026-10-21). Issue #5 (also closes the deferred part of #7).
- **Context:** The graph must support more than 10,000 people without loading the whole graph (PRD 08, PRD 09). Only active links take part in traversal and derivation (PRD 03). The cycle check counts all non-deleted links (PRD 02).
- **Options considered:** PostgreSQL, SQL Server or SQLite; Testcontainers or a shared local database for integration tests; recursive CTEs or per-generation expansion in Application; a stored status column, a database-computed status, or status derived from per-endpoint approval columns; a hard depth limit or a node cap for finding how two people are related.
- **Trade-offs:** PostgreSQL is free, easy to self-host and supports recursive CTEs. Testcontainers tests the real engine but requires Docker locally and in CI. Recursive CTEs need hand-written SQL but take one round trip per query. Deriving state from the two approval columns keeps a single source of truth, at the cost of a slightly longer filter predicate.
- **Decision:**
  - **Database:** PostgreSQL with EF Core, via `Npgsql.EntityFrameworkCore.PostgreSQL`.
  - **Integration tests:** Testcontainers (`Testcontainers.PostgreSql`, test projects only) starts a disposable PostgreSQL for the run. In-memory and SQLite providers are rejected because they do not reproduce PostgreSQL constraints, transactions or recursive queries.
  - **Traversal:** recursive CTEs with depth and row limits inside the query, hand-written behind Application's query interfaces.
  - **Link state storage:** the relationship row stores the parent-side and child-side approval (approved, pending or disagreed) and a nullable deletion timestamp. No separate status column. A link is active when both sides are approved and it is not deleted. When one user maintains both endpoints, both sides start approved.
  - **Limits** (configurable per instance; the maximums are enforced in code):

    | Query | Default | Maximum |
    |---|---|---|
    | Local view around a person, generations each way | 2 | 4 |
    | Local view or "load more" from an edge person, people returned | 10 | 500 |
    | How two people are related | no generation limit; at most 10,000 people examined | configurable |
    | Cycle check at link creation or reactivation | 5,000 ancestors / 50 generations | same |

    All graph queries also run under a query timeout. A query that hits a limit returns what it found and says it was cut short. "How two people are related" reports "no connection found within the search limit" rather than "not related". The cycle check rejects the link instead (PRD 02).
  - **Indexes:** a unique index on (parent, child) for non-deleted links; parent and child lookup indexes filtered to active links; a child lookup index over all non-deleted links for the cycle check.
- **Consequences:**
  - Docker is required to run integration tests locally and in CI (#34).
  - Future maintainer checkpoints may change the people-examined cap for relationship search.
  - Package versions are pinned when #9 adds them.

### D-010: API conventions

- **Status:** agreed (2026-10-21). Issue #6.
- **Context:** The Client shows graphs, mainly fan charts centred on one person, not tables. Privacy must be enforced on every read path (PRD 05). Domain rules throw custom exceptions (D-007).
- **Options considered:** minimal APIs or MVC controllers; API models in Application or Server; built-in validation or FluentValidation; page-number, cursor or no pagination; ProblemDetails or a custom error shape; one privacy component or per-query privacy filtering.
- **Trade-offs:** Controllers are more ceremony than minimal APIs but familiar to contributors. Separate API models duplicate some types and need mapping, but keep HTTP-specific validation out of Application. Skipping pagination is simpler but needs a limit on every list. One privacy component can't be skipped by a new endpoint, while per-query filtering repeats the rule in every query.
- **Decision:**
  - **Endpoints:** MVC controllers with `[ApiController]`, one controller per feature. Routes are plural and lowercase under `/api` with no version prefix (e.g. `/api/people`, `/api/relationships`).
  - **API models:** request and response models live in Server and carry API-specific validation. Server maps them to and from Application's use-case inputs and read models. EF entities and domain objects are never API contracts.
  - **Validation:** built-in data annotations on the Server API models, checked automatically by `[ApiController]` (400). Business rules stay in the domain (D-007). No validation library.
  - **Graph reads, no pagination:** a view request returns the centre person plus up to N related people (default 10, configurable, hard maximum 500, see D-009). Edge people whose further relatives were not loaded are flagged "has more", and a "load more" request around an edge person returns up to the same limit.
  - **Lists, no pagination for now:** person search returns the top 20 matches and says when there are more ("refine your search"). Other lists (e.g. profiles I maintain, audit history) return everything up to a configurable limit and say when they were cut short. Pagination is added only when a real list needs it.
  - **Errors:** ProblemDetails (RFC 9457), with each custom exception's stable error code as an extension so the Client can choose wording or translations. Status codes: 400 invalid request shape, 403 not permitted (e.g. not the maintainer), 404 not found, 409 conflict with current state (e.g. duplicate link), 422 domain rule violated (e.g. cycle, missing first name).
  - **Privacy projection:** one Application component turns hidden profiles into `Private` or `Deleted` placeholders. Query services return person references that only this component can produce, so a read path cannot skip it. Integration tests cover every read endpoint. What is hidden is decided in #17.
- **Consequences:** Every custom exception carries an error code (D-007). A global exception handler in Server maps exceptions to ProblemDetails. The "has more" flag needs a cheap existence check per edge person in the traversal query. The fan-chart presentation is decided with #19 and #23.

### D-011: Production hosting and deployment

- **Status:** agreed (2026-10-21). Issue #33 (decision part; the implementation stays in #33).
- **Context:** Self-hosting without a specific provider is a product goal (PRD 09). Sign-in is Google via ASP.NET Core Identity (D-008). The SPA Proxy is for development only.
- **Options considered:** Server serves the built Client, or the Client is hosted separately with token auth; one image plus a compose file, or the image only; a separate migration step, or migrations at app startup.
- **Trade-offs:** Serving the Client from Server is one process and one origin, so no CORS and cookie sessions work naturally, but the frontend cannot scale separately. A compose file is one more file to maintain, but gives self-hosters a one-command setup. A separate migration step is one more container, but migrations run once and fail cleanly.
- **Decision:**
  - **Client hosting:** Server serves the production Angular build as static files from the same origin, with a fallback to `index.html` for client routes. The SPA Proxy stays development-only.
  - **Sessions:** ASP.NET Core Identity cookie authentication. The cookie is `HttpOnly`, `Secure` and `SameSite=Lax`, and state-changing API requests are protected against CSRF (antiforgery). No bearer tokens in the browser.
  - **Artifact:** one Docker image containing Server and the built Client, plus `deploy/docker-compose.yml` that runs the app, PostgreSQL and the migration step. Configuration (Google client, admin emails, connection string, limits) comes from environment variables.
  - **Migrations:** an EF Core migration bundle runs as a separate one-off step before the app starts; the app does not migrate at startup. A failed migration stops the deploy.
- **Consequences:** #33 adds the Dockerfile, `deploy/` and self-hosting docs. HTTPS is terminated by the operator's reverse proxy or host; the docs explain this. #34 builds and publishes the image.

### D-012: CI/CD, configuration and releases

- **Status:** agreed (2026-10-21). Issue #34 (decision part; the implementation stays in #34).
- **Context:** The repository is on GitHub. Integration tests need Docker (D-009). Self-hosters deploy a Docker image (D-011) and must supply their own Google client and admin emails (D-008).
- **Options considered:** GitHub Actions or another CI service; environment variables or committed per-instance config; tagged semantic-version releases to a container registry, or build-from-source only.
- **Trade-offs:** GitHub Actions keeps CI next to the code and its hosted runners include Docker. Publishing images means maintaining a release process, but self-hosters can pin versions instead of building.
- **Decision:**
  - **CI:** one GitHub Actions workflow on every PR and on pushes to `main`. Backend: `dotnet build` and `dotnet test` (architecture, unit and Testcontainers integration tests). Client: `npm ci`, Prettier check, `npm test`, `npm run build`. Playwright smoke tests join when the first UI flow exists (D-006).
  - **Configuration:** standard ASP.NET Core configuration. `appsettings.json` holds safe defaults only. Instance-specific values (Google client ID and secret, admin emails, connection string, query limits) come from environment variables, listed in a documented `deploy/.env.example`. Secrets are never committed. Local development uses `dotnet user-secrets`.
  - **Releases:** semantic versions from Git tags (`vX.Y.Z`). A pushed tag builds the Docker image and publishes it to GitHub Container Registry (`ghcr.io/open-source-nep/familygraph`). Self-hosters pin a version in the compose file.
- **Consequences:** #34 adds the CI workflow first and the release workflow after #33's Dockerfile. Required status checks on `main` are a repository setting for the maintainers.

### D-013: Consent state transitions

- **Status:** agreed (2026-10-22). Issue #15.
- **Context:** PRD 03 sets the consent rules and leaves the state machine to implementation. D-009 fixes three per-endpoint states (approved, pending, disagreed) and derives "active" from them. Person has no claim status yet (#25). Every transition will write a consent audit event (#10).
- **Options considered:** disagreement open to any endpoint maintainer, or to claimed persons only; allowing a pending endpoint to decline, or only to approve; a reset back to pending; rejecting repeated actions, or ignoring them; letting a sole maintainer of both endpoints disagree, or requiring deletion.
- **Trade-offs:** Limiting disagreement to claimed persons needs a claim flag before #25 and a separate withdraw state. Without decline, a refusal leaves no record. A reset to pending has no basis in the PRD. Silently ignoring repeats hides client bugs and risks empty audit events.
- **Decision:**
  - Two operations, each on one endpoint and only by that endpoint's current maintainer (`link.consent_not_maintainer`). Nobody else, including administrators, can change an endpoint.
  - **Approve:** pending → approved, or disagreed → approved (restoring approval). The other side's valid approval is kept. If the approval makes the link active, the cycle check runs first (PRD 02).
  - **Disagree:** approved → disagreed (disagreement or withdrawal), or pending → disagreed (declining). The link is inactive immediately. Withdrawal and disagreement are the same transition; #10 and #16 distinguish them in the audit history where needed.
  - No transition returns to pending.
  - A request for the state an endpoint is already in is rejected (`link.consent_unchanged`).
  - A user who maintains both endpoints cannot disagree; they delete the link instead (#16) (`link.sole_maintainer_must_delete`).
  - Claims and merges have no operation on a link, so they cannot change consent.
- **Consequences:** `EndpointApproval` gains `Disagreed` (value 3). `Approve` takes the parent's ancestor snapshot, which Application supplies. Audit events for each transition come with #10; Application, API and Client parts follow later.

### D-014: Link removal

- **Status:** agreed (2026-10-22). Issue #16.
- **Context:** PRD 03 lets a user who maintains both endpoints delete a link, and makes withdrawing approval the only option when different users maintain them. D-013 already provides withdrawal as Disagree, and D-009 stores deletion as a nullable timestamp. PRD 03 says the sole maintainer may delete "the active relationship", but a link can be inactive by the time one account maintains both endpoints (administrator inheritance, succession, claims).
- **Options considered:** deletion in any consent state; deletion of active links only (an inactive link must be approved first); deletion unless a side has disagreed.
- **Trade-offs:** Deleting never activates a link, so it cannot override consent, and the audit history keeps every earlier state. Requiring approval first adds a brief, artificial activation. Blocking on disagreement leaves unwanted links stuck.
- **Decision:**
  - **Delete:** only an account that maintains both endpoints, in any consent state (`link.not_sole_maintainer` otherwise). It sets the deletion timestamp. The link and its consent state are kept for history, and a deleted link is never active.
  - Deletion is final. To undo it, create the link again, which gives a new link.
  - A deleted link rejects every further operation (`link.deleted`). The authority check runs first, so non-maintainers do not learn that the link was deleted.
  - Maintainers on different accounts remove a link by withdrawing approval (Disagree, D-013), which they can later restore.
  - Delete and Disagree are separate operations, so their audit events (#10) are distinct.
- **Consequences:** `ParentChildLink` gains `Delete`, `DeletedAt` and `IsDeleted`; Application supplies the timestamp. Audit events come with #10; Application, API and Client parts follow later.
