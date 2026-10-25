# Open Technical Decisions

The technical decision log, plus any unresolved choices. Do not treat recommendations as final until discussed and agreed.

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
- Write application code only for scoped tasks the user authorizes.
- Repository layout, toolchain versions, and styling approach (D-001 below).
- Project references and persistence abstractions (D-002 below).
- **First implementation slice (agreed 2026-10-21):** domain core only, in `FamilyGraph.Business`. It covers people, maintainers, parent–child links, per-endpoint consent and audit events, with unit tests. It uses no persistence, identity or HTTP, so it is not blocked by the decisions below.
- **Unit-test framework (agreed 2026-10-21):** xUnit v3. The integration-test database is Testcontainers PostgreSQL (D-009).
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
- Audit event model (D-015 below).
- Birth order of children (D-016 below).
- Persistence mapping (D-017 below).
- Accounts, sign-in flow and sessions (D-018 below).
- Person profile use cases, unit of work and API (D-019 below).
- Creating parent–child links: use case, ancestor query and API (D-020 below).
- Link approvals: use cases, inactive-links view and API (D-021 below).
- Link deletion: use case, API and Client (D-022 below).
- Graph exploration: local view, connection search and privacy projection (D-023 below).
- Client graph view: rendering library and first slice (D-024 below).
- Profile privacy: visibility, the viewer, placeholders on every read path (D-025 below).
- Profile deletion: erasure, the `Deleted` placeholder and frozen consent (D-026 below).
- Voluntary profile handover: hashed one-time links, accept and cancel (D-027 below).
- Profile images: a blob in PostgreSQL, resized in the browser (D-028 below).
- Data export: an administrator's streamed JSON file of the whole instance (D-029 below).
- Genders on profiles (D-030 below).
- Marriages (D-031 below).
- Adding relatives from the graph, with person search (D-032 below).
- Data import: an administrator's upload of an export file into an empty instance (D-033 below).
- Deleting profiles from the graph (D-034 below).
- Administrator Users page and profile transfers (D-035 below).
- Linking spouses too, and choosing relatives on a new profile (D-036 below).
- Descriptions, birth dates and setting birth order (D-037 below).
- One people picker with avatars and descriptions, and People page rows (D-038 below).
- Children in birth order in the graphs (D-039 below).
- Administrators approving pending sides on behalf of maintainers (D-040 below).
- Enlarging the profile photo (D-041 below).
- All people on the People page, paged (D-042 below, proposed).
- **Name length (agreed 2026-10-21):** Business puts no length cap on name parts. Any storage or input limit is set later by persistence (#9) and API validation (D-010).

## Discussion topics (all agreed)

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
- **Implementation (2026-10-24, part of #33):** `src/Server/Dockerfile` builds with `src/` as the context in three stages: Node 24 runs the production Angular build, the .NET 10 SDK publishes Server with that output copied into `wwwroot`, and the `aspnet:10.0` runtime runs it as the non-root `app` user on port 8080. Server serves `wwwroot` with `index.html` as the fallback for every non-file path outside `/api/`. Setup: `docs/deployment.md`. The image contains the migration bundle `./efbundle`; with `FAMILYGRAPH_MIGRATE=true` the entrypoint runs it before the app and stops on failure (agreed 2026-10-24, Ash, for hosts without one-off steps such as Render's free plan). `deploy/` and the compose file are still to come.

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
- **Amendment (agreed 2026-10-24, Ash):** publishing is manual for now. `.github/workflows/publish.yml` runs only from the Actions tab (`workflow_dispatch`) and pushes `ghcr.io/open-source-nep/familygraph` tagged `latest`, `sha-<commit>` and, when given, a version such as `1.2.0`. Publishing on pushed `vX.Y.Z` tags can be added later.

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

### D-015: Audit event model

- **Status:** agreed (2026-10-22). Issue #10.
- **Context:** PRD 07 requires append-only audit events for every lifecycle change, kept after the active row is deleted, with claim decisions separate from consent events. D-002 saves audit events in the same transaction as the change. PRD 05 replaces erased personal data in audit records with `[erased]`. Event names are left to implementation (PRD 03).
- **Options considered:** where events come from (entities record them, Application use cases write them, or each operation returns its event); whether events copy personal values such as old and new names.
- **Trade-offs:** Use-case-written events can be forgotten, and only tests catch that. Returned events can be ignored by the caller. Copying personal values shows what changed but needs rewriting on erasure and keeps more personal data.
- **Decision:**
  - Business entities (`AuditedEntity`) record an `AuditEvent` in every successful operation. Rejected operations record nothing. Application takes the events with `TakeEvents()`, stamps the time and saves them with the change.
  - Each event has a family (`AuditFamily`: relationship consent, identity claim, profile, merge, administrative), a type (its record type), subject IDs and the acting account.
  - Events hold IDs, types and states only, never names or other personal values. A rename records that it happened, not the names.
  - Consent events, one per transition: `LinkCreated`, `LinkEndpointApproved` (pending → approved), `LinkApprovalRestored` (disagreed → approved), `LinkEndpointDeclined` (pending → disagreed), `LinkEndpointDisagreed` (approved → disagreed, which includes withdrawal), `LinkDeleted`. Profile events: `PersonCreated`, `PersonRenamed`, `LinkBirthOrderChanged` (D-016).
  - Claim, merge and administrative event types are added with their features. Restricted payloads (claim evidence, admin rationale) are stored apart from general events, so general audit reads cannot reach them.
- **Consequences:** Persistence (#9) stores events in an append-only table keyed by family, type, subject and actor, with no foreign key to the active rows so records outlive deletion. Visibility and the reconstruction query come with #27 and #28.

### D-016: Birth order of children

- **Status:** agreed (2026-10-22). Issue #48.
- **Context:** Ash requires that the order of a parent's children matters. PRD 02 now defines it as a display-only birth order, set manually per parent by the parent's maintainer, with new children unordered. Profiles store no birth date, and a parent's children can include half siblings with a different other parent.
- **Options considered:** a number field on each parent–child link, set one link at a time; a separate service that replaces a parent's whole order with contiguous positions.
- **Trade-offs:** A number on the link needs no new class and keeps the rule on the aggregate that owns it. It does not keep numbers unique or contiguous across siblings, which a sort key does not need. A whole-order service guarantees that, but adds a class and needs all of the parent's links for every change (rejected in PR #49 review).
- **Decision:**
  - `ParentChildLink.BirthOrder` is a nullable number (lower is elder). `null` means unordered, and every new link starts unordered.
  - `ParentChildLink.SetBirthOrder(parent, actor, number)` sets or clears it. Only the parent's maintainer may (`link.birth_order_not_parent_maintainer`). The number must be 1 or greater (`link.birth_order_invalid`). Setting the current value is rejected (`link.birth_order_unchanged`), and a deleted link rejects it (`link.deleted`).
  - Setting it never touches consent or activity, so pending and inactive links can be placed.
- Every change records a `LinkBirthOrderChanged` audit event (D-015) with the link, the parent and the new number. It is in the profile family, not relationship consent, because it needs no consent.
- **Consequences:** Read models sort a parent's children by number, then by name for equal numbers and unordered children. Application, API and Client parts follow later.

### D-017: Persistence mapping

- **Status:** agreed (2026-10-22). Issue #9, PR #51.
- **Context:** D-009 picks PostgreSQL and EF Core and fixes the link columns and indexes. D-015 asks for an append-only audit table keyed by family, type, subject and actor. Business must not depend on EF Core (D-002), and its entities have private constructors and `BaseEnum` values. Identity packages arrive with #11 (D-008).
- **Options considered:** mapping the domain entities directly, or separate persistence classes copied to and from the domain; the `EFCore.NamingConventions` package or a few lines of our own for snake_case names; audit event fields as columns per type, or as JSON.
- **Trade-offs:** Separate persistence classes leave Business untouched but duplicate every entity and need hand-written change tracking. Direct mapping needs one private constructor on `Person`, because EF Core cannot pass a complex property to a constructor. A naming package is one more dependency for about thirty lines of code. Columns per event type would change the schema with every new event; JSON keeps one table and the indexed columns stay typed.
- **Decision:**
  - EF Core maps the domain entities directly, with fluent configurations in Infrastructure only. `Person` gains a private parameterless constructor for materialization; Business still references nothing.
  - Typed IDs are `uuid`. `BaseEnum` values are stored by their integer `Value`, with check constraints limiting each column to the declared values. The database also rejects self-links and birth-order numbers below 1.
  - Name parts are `text` with no length limit in the database; any input limit is API validation (D-010).
  - Columns, keys and foreign keys get lower snake_case names from a small convention in Infrastructure; tables and indexes are named in their configurations.
  - `audit_events` has `id` (UUID v7), `occurred_at`, `family`, `type` (the event record's name), `actor_id`, `subject_id` (the link or person) and `data` (`jsonb` with the event's other fields; IDs as strings, `BaseEnum` values as integers). It has no foreign keys. Every audit event type must name its subject, and a test checks this.
  - The context is a plain `DbContext` until #11 derives it from `IdentityDbContext`. It is internal to Infrastructure; Server calls `AddInfrastructure` with its configuration; the connection is built from the `Database` section, one Npgsql keyword per key (changed from the `ConnectionStrings:FamilyGraph` string on 2026-10-22).
  - Packages: `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3, `Microsoft.EntityFrameworkCore.Design` 10.0.4 (build-time only), the `dotnet-ef` 10.0.4 local tool, and `Testcontainers.PostgreSql` 4.16.0 in `FamilyGraph.Infrastructure.Tests`. Tests and local docs use the `postgres:18-alpine` image.
  - Integration tests share one container per run and give each test its own migrated database.
- **Consequences:** Store interfaces, `IUnitOfWork` and query services come with the first Application use cases, not #9. Local setup is in `docs/database.md`.

### D-018: Accounts, sign-in flow and sessions

- **Status:** agreed (2026-10-22). Issue #11, PR #52. Ash chose the account data; the rest applies D-008, D-010 and D-011.
- **Context:** D-008 picks Google sign-in, ASP.NET Core Identity, the `User` and `Administrator` roles and config-defined admins. D-011 picks an Identity session cookie with CSRF protection. The product keeps personal data to a minimum, and an account is never a person profile (PRD 01).
- **Options considered:** keeping only the email from Google, or also the name, or also the photo; a separate registration step, or registering on first sign-in; Identity's `SignInManager`, or signing in with the Identity cookie scheme directly.
- **Trade-offs:** A name or photo would let the UI greet the user, but is more personal data to protect and erase, and no requirement needs it; a user's name belongs on their Person profile. A separate registration step adds a screen with nothing to ask. `SignInManager` brings password, lockout and security-stamp features that Google-only sign-in does not use and that need more configuration.
- **Decision:**
  - **Account data:** an account stores its Google user ID (as an Identity external login), its email and whether Google verified it. Nothing else. Sign-in requests only the `openid` and `email` scopes. The email is refreshed on every sign-in.
  - **Registration:** the first Google sign-in creates the account with the `User` role. It never creates a Person (`AC-01.12`).
  - **Administrators:** on every sign-in the account gets or loses `Administrator` by matching its verified email, ignoring case, against `Admin:Emails` (D-008).
  - **Identity in Infrastructure:** `FamilyGraphDbContext` derives from `IdentityDbContext` with GUID keys. Tables are `user_accounts`, `user_account_logins`, `user_account_roles`, `user_account_claims`, `user_account_tokens`, `roles` and `role_claims`; the two roles are seeded by migration. Application owns `IAccountStore` and `AccountService`; Infrastructure implements the store with `UserManager`. The account ID is the domain's `AccountId`.
  - **Endpoints** (`AccountController`, `/api/account`): `POST /api/account/google-sign-in` checks a Google ID token, creates or updates the account and starts the session (204; 503 `account.sign_in_unavailable` when Google is not configured; 401 `account.sign_in_failed` for a rejected token); `POST /api/account/sign-out` ends it; `GET /api/account` returns the own account or 401. *(Revised 2026-10-22, see "Client-side Google flow" below.)*
  - **Session:** the Identity application cookie `FamilyGraph.Session` (`HttpOnly`, `Secure`, `SameSite=Lax`, sliding expiry) holds the account ID and roles. The API answers 401 and 403 instead of redirecting.
  - **CSRF:** `GET /api/account` sets a readable `XSRF-TOKEN` cookie. Angular's `HttpClient` sends it back as `X-XSRF-TOKEN`, and a global filter rejects any non-GET request without a valid token (400 `request.antiforgery_invalid`).
  - **Client:** a sign-in page, an account page with sign-out, and a guard that sends signed-out users to sign-in with a `returnUrl`. Since #55 the Client loads `GET /api/configuration` once at startup (`provideAppInitializer`); it reports whether Google sign-in is configured and, since the revision below, the public client ID. The sign-in page shows a Google-branded button (inline logo, no Google script or font) that starts the Google flow, says when Google is not configured or the server is unreachable, and sends already signed-in users to their local `returnUrl`. In development, the Angular dev server serves HTTPS with the ASP.NET Core development certificate. Since 24cd6b5 its proxy is `src/client/src/proxy.conf.js`, which forwards `/api` and `/hubs` (WebSockets) to the Server and no longer sends `X-Forwarded-*` headers; since the client-side flow below, sign-in does not need them.
  - **Google redirect URI (revised 2026-10-22, at Ash's request):** Google redirects to the Client route `/google-callback`, not to a Server path, so the dev proxy forwards no sign-in paths. The redirect URI to register is `https://<host>/google-callback`. HTTPS redirection runs outside Development only. *(The Server-side handler from the first revision, `ClientCallbackGoogleHandler`, was superseded the same day by the next point.)*
  - **Client-side Google flow (revised 2026-10-22, at Ash's request):** the Client runs the Google side; the Server only checks the result.
    - *Options considered:* the redirect flow with `response_type=id_token` built by the Client; Google Identity Services (Google's script and button, popup); the code flow started by the Client with the Server exchanging the code. For validation: `Google.Apis.Auth` or `Microsoft.IdentityModel.Protocols.OpenIdConnect`.
    - *Trade-offs:* Google Identity Services loads a third-party script on our pages. The code flow still needs the client secret and a Server call to Google. The id_token flow needs neither, but the token passes through the browser, so it must be bound to the browser that asked for it. `Google.Apis.Auth` is one call maintained by Google but brings `Google.Apis.Core` and `Newtonsoft.Json`. Ash chose it over `Microsoft.IdentityModel`.
    - *Decision:* `GET /api/configuration` returns `google.clientId` (public). The sign-in button sends the browser to Google with `response_type=id_token`, `scope=openid email`, a random `state` and `nonce` = base64url SHA-256 of a random secret, all kept in session storage. `/google-callback` reads the token from the URL fragment, checks `state` (each pending sign-in is used once), and posts `{ idToken, nonceSecret }` to `POST /api/account/google-sign-in`. The Server validates the token with `GoogleJsonWebSignature` (signature, issuer, expiry, audience = client ID) and checks the nonce against the secret, so a leaked token cannot be replayed without it. The login provider stays `Google`, so existing accounts are found again. `GET /api/account` also issues the antiforgery cookie when signed out, so the sign-in POST passes the global antiforgery filter.
    - *Consequences:* no client secret is configured anywhere; the external sign-in cookie and the Google authentication handler are gone. The Client builds the redirect URI from the browser's origin, so forwarded headers no longer matter for sign-in.
  - **Packages:** `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.4 (matches EF Core) in Infrastructure. `Google.Apis.Auth` 1.77.0 in Server replaces `Microsoft.AspNetCore.Authentication.Google` (named in D-008) since the revision above.
- **Consequences:**
  - The roles in a session are those of its sign-in. An administrator removed from `Admin:Emails` keeps the role until signing in again or the session expires. The first administrator-only endpoint decides whether to re-check per request.
  - Self-hosters set `Authentication__Google__ClientId` and `Admin__Emails__0` (see `docs/authentication.md`). No client secret is needed.
  - Account deletion and the erasure of the stored email come with #32.

### D-019: Person profile use cases, unit of work and API

- **Status:** agreed (2026-10-22). Issue #12, PR #53.
- **Context:** #12 is the first Application use case, so it brings the store interfaces, `IUnitOfWork` and query services that D-002 and D-017 deferred. D-015 has Application take and time-stamp audit events. D-010 sets the API shape, and D-017 leaves name length limits to API validation. Privacy settings and the `Private` placeholder come with #17 (PRD 05); until then every profile is Public, the PRD 05 default.
- **Options considered:** saving audit events by scanning EF Core's change tracker in `SaveChanges`, or Application adding them to an audit log; whether to return a profile's maintainer account to other users; a length limit per name part of 100 or none.
- **Trade-offs:** The change-tracker approach cannot forget events, but hides the time stamp and the event list in Infrastructure, against D-015. Naming the maintaining account to others reveals which account manages which profiles, which no requirement needs.
- **Decision:**
  - **Write side:** Application owns `IPersonStore` (find, add), `IAuditLog` (add events with their time) and `IUnitOfWork` (save). `PersonService` takes the entity's events, stamps them with `TimeProvider` and saves change and events in one `SaveChanges`, so one transaction.
  - **Read side:** `IPersonQueries` returns `PersonView` (ID, name, whether the viewer maintains it) and the maintained list sorted by first, middle and last name, cut at `People:MaintainedListLimit` (default 500) with an `IsTruncated` flag.
  - **Errors:** Business adds `NotPermittedException` (a `DomainException`) for authority rules; `NotProfileMaintainerException` derives from it. Application adds `NotFoundException`. A Server `IExceptionHandler` maps them to ProblemDetails with the `code` extension: not permitted 403, not found 404, other domain rules 422.
  - **Endpoints** (`PeopleController`, signed-in users only): `POST /api/people` (201 with the profile), `GET /api/people/{id}`, `PUT /api/people/{id}/name` (204, maintainer only), `GET /api/people/maintained`. Any signed-in user may view any profile; responses say `maintainedByYou` but never name the maintaining account.
  - **Validation:** each name part is at most 100 characters (400). A missing or blank first name is the domain rule `person.first_name_required` (422).
  - **Client:** `features/people` has the profiles-you-maintain list, an add-a-person page and a person page where the maintainer edits the name.
- **Consequences:** Later use cases reuse `IAuditLog` and `IUnitOfWork` with their own stores. #17 puts the privacy projection in front of `IPersonQueries` before any Private profile exists. The link permission exceptions move to `NotPermittedException` with their API work (#14, #15, #16).

### D-020: Creating parent–child links

- **Status:** agreed (2026-10-22). Issue #14, PR #54.
- **Context:** The domain already decides who may create a link, the starting approvals, duplicates and cycles (PRD 02, PRD 03, D-013). #14 brings the use case, the bounded ancestor query D-009 asks for, the API and the Client. Person search (#22) and the local graph view (#20, #23) come later, and privacy placeholders come with #18.
- **Options considered:** a recursive CTE for ancestors, or one query per generation from Application; returning a new link's per-endpoint approvals, or only whether it is active; showing pending links on a person's page, or active ones only; picking the other person by search, by pasted ID, or from the profiles you maintain.
- **Trade-offs:** A recursive CTE is one round trip, and taking the union over (person, generation) visits each person at most once per generation, so pedigree collapse cannot blow it up. Showing only active links follows PRD 03 ("excluded from ordinary relationship views"); approving pending links is #15. Picking only from your own profiles needs no search, and still covers every allowed link, because the creator must maintain one endpoint: open the other person's page and link one of your profiles to it.
- **Decision:**
  - **Write side:** Application owns `IParentChildLinkStore` (non-deleted parents of a child, the ancestor snapshot, add). `RelationshipService.CreateLinkAsync` loads both people (404 `person.not_found` if either is missing), the child's parents and the parent's ancestors, calls `ParentChildLink.Create` and saves the link with its `LinkCreated` event in one transaction.
  - **Ancestor query:** one recursive CTE over non-deleted links that walks one generation past `MaxGenerations` and returns one ancestor past `MaxAncestors`; either extra row makes the snapshot incomplete, so the link is rejected as unverifiable (AC-02.7). It also stops on a cycle that is already in the data.
  - **Errors:** Business adds `ConflictException` (409, D-010). `DuplicateLinkException` derives from it and `NotLinkEndpointMaintainerException` from `NotPermittedException` (403). A unique-index violation from two simultaneous creations of the same pair is also reported as `link.duplicate`. Self-links and cycles stay 422.
  - **Read side:** `IRelationshipQueries` lists a person's parents (by name) and children (birth order, then unordered by name, D-016) through active links only, each with its type and each cut at `Relationships:DirectRelativesLimit` (default 500) with a truncation flag.
  - **Endpoints** (`RelationshipsController`, signed-in users only): `POST /api/relationships` with `parentId`, `childId` and `type` (`Biological` or `Adoptive`, otherwise 400) answers 201 with the link's ID, endpoints, type and `isActive`. It does not return per-endpoint approvals. `GET /api/people/{id}/relatives` returns the parents and children.
  - **Client:** `features/relationships` adds the parents and children to the person page, with "adoptive parent" and "adopted child" labels (PRD 10), and a form that links one of your profiles as a parent or child of the person shown. A link that is not active yet is reported as waiting for the other maintainer's approval.
- **Consequences:** #15 adds the list of links waiting for your approval and the approve and disagree endpoints. #18 blocks new links to a `Deleted` profile and puts placeholders on the relatives list. Two simultaneous requests that each add half of a cycle (A → B and B → A) can both pass the check, because each transaction cannot see the other; graph traversal already tolerates cycles (PRD 02). Closing that gap needs serializable transactions or a lock, which is left until it is seen in practice.

### D-021: Link approvals end to end

- **Status:** agreed (2026-10-22). Issue #59, the Application, API and Client parts of #15.
- **Context:** D-013 fixes the consent transitions in the domain and D-015 their audit events. #15's acceptance asks that a user sees the links of the profiles they maintain and can approve, decline, withdraw or restore. PRD 03 keeps inactive links out of ordinary relationship views, and PRD 07 lets the current maintainers of both profiles see a link's audit history. Deletion by a sole maintainer stays with #16.
- **Options considered:** naming the endpoint by person ID or by side (`Parent`/`Child`); letting the server infer the side from the actor; listing only links waiting for you, or every inactive link of your profiles; showing the other side's state or hiding it; status codes for repeated and unauthorised changes.
- **Trade-offs:** A person ID needs an extra "not an endpoint" error; inferring the side fails when one account maintains both people (inheritance). Listing only pending links leaves no way to find a link you withdrew in order to restore it. Showing the other side's state reveals no more than the audit history PRD 07 already gives both maintainers, and tells you why a link is still inactive.
- **Decision:**
  - **Write side:** `IParentChildLinkStore.FindAsync` loads a link (deleted or not, so the domain reports `link.deleted`). `RelationshipService.ApproveAsync` and `DisagreeAsync` take the link, the side (`LinkEndpoint`) and the actor, load both people and, for approval, the parent's ancestor snapshot, so a reactivation re-runs the bounded cycle check (AC-02.6). The change and its audit event are saved in one transaction. An unknown link is 404 `link.not_found`.
  - **Errors:** `link.consent_not_maintainer` becomes a `NotPermittedException` (403) and `link.consent_unchanged` a `ConflictException` (409). `link.sole_maintainer_must_delete` stays 422.
  - **Read side:** `IRelationshipQueries.ListInactiveLinksAsync` returns the non-deleted, inactive links with an endpoint the viewer maintains: those with a pending endpoint of the viewer's first, then by parent and child name, cut at `Relationships:InactiveLinksLimit` (default 500) with a truncation flag. Each link shows both people, both approvals and which endpoints the viewer maintains, never the other maintaining account. The relatives list also returns each link's ID and whether the viewer maintains the relative, so an active link can be withdrawn from the person page.
  - **Endpoints:** `GET /api/relationships/inactive`; `POST /api/relationships/{id}/approve` and `POST /api/relationships/{id}/disagree` with `{ "endpoint": "Parent" | "Child" }` (400 otherwise), each answering 200 with the link's consent state.
  - **Client:** an Approvals page (`/approvals`, in the header) groups the links into "waiting for your approval" (Approve, Decline), "declined or withdrawn by you" (Restore approval) and "waiting for others" (why it is inactive). The person page offers "Withdraw approval", with an inline confirmation, on each relative where you maintain exactly one of the two people.
- **Consequences:** Two maintainers approving opposite sides at once both succeed, since each saves only its own column. A concurrent approval and a link closing a cycle have the same gap as D-020. #16 adds deletion next to these actions, and #18 puts placeholders on the inactive-links list.

### D-022: Link deletion end to end

- **Status:** agreed (2026-10-22). Issue #61, the Application, API and Client parts of #16.
- **Context:** D-014 fixes deletion in the domain: only an account that maintains both endpoints deletes, in any consent state, finally, and a deleted link rejects every further change. D-021 put withdrawal on the person page and the Approvals page and left deletion to #16. D-015 already records `LinkDeleted`, distinct from the consent events.
- **Options considered:** `DELETE /api/relationships/{id}` or a `POST .../delete` action next to approve and disagree; 200 with the link or 204; the status of `link.deleted` (409 conflict, 404 or 410 gone, or 422 as before); where the Client offers deletion.
- **Trade-offs:** `DELETE` is the plain HTTP verb for removal, and no state is left for the caller to show. 404 or 410 would tell a non-maintainer that the link existed and was deleted, which D-014 hides by checking authority first; 409 matches `link.consent_unchanged` as a change that conflicts with the link's current state.
- **Decision:**
  - **Write side:** `RelationshipService.DeleteAsync` loads the link (deleted or not) and both people, deletes it with the current time and saves the change with `LinkDeleted` in one transaction. Deleted links already leave the relatives list, the inactive-links list, and the duplicate and ancestor checks, so the pair can be linked again as a new link.
  - **Errors:** `link.not_sole_maintainer` becomes a `NotPermittedException` (403) and `link.deleted` a `ConflictException` (409). An unknown link is 404 `link.not_found`. `link.birth_order_not_parent_maintainer` also becomes a `NotPermittedException`, so every "not the maintainer" error is 403.
  - **Endpoint:** `DELETE /api/relationships/{id}`, 204 No Content.
  - **Client:** "Delete link", with an inline confirmation that says deletion cannot be undone, on each relative of the person page where you maintain both people (where "Withdraw approval" shows when you maintain only one), and on each Approvals-page link where you maintain both people.
- **Consequences:** Deleting a link a moment after the other maintainer's account took over one endpoint fails with 403, and the Client shows withdrawal on the next load. The derived-relationship part of #16's acceptance is checked once #21 exists.

### D-023: Graph exploration API

- **Status:** agreed (2026-10-23). Issue #20, PR #63. The choice of people in a local view was agreed with the user on 2026-10-22.
- **Context:** D-009 fixes the limits (2 generations each way, at most 4; 10 people, at most 500; 10,000 people examined for "how are two people related"; a query timeout) and recursive CTEs. D-010 fixes "has more" and "load more" instead of pagination, and one privacy component for every read path. PRD 08 asks for the shortest path plus up to 5 more. Privacy settings and deleted profiles come with #18, so every profile is Public for now (PRD 05). Path explanations, labels and adoption wording are #21; the visual view is #23.
- **Options considered:** which people fill a local view (nearest first in any direction, ancestors and descendants only, or a direction chosen by the client); walking in recursive CTEs or per generation in Application; finding paths in SQL or in a pure Business service over a bounded set of links; how "more paths" are counted; a command timeout or a cancelled query.
- **Trade-offs:** Nearest-first shows siblings and the other parent of your children in the first view, at the cost of a fan or tree layout needing to place them. Paths in SQL would need path arrays per walk, which grow fast in a family graph; loading the links between the nearest 10,000 people and searching them in memory keeps the CTE simple, and every shortest path to a reached person lies inside that set. A Npgsql command timeout applies per read, so a walk that streams rows slowly would never hit it.
- **Decision:**
  - **Local view (agreed):** the centre and the people nearest to it in any direction over active links, closest first (parents, children, then siblings, grandparents, grandchildren and so on), within `Graph:ViewGenerations` generations above or below the centre. People of the same distance are ordered by the closest generation, then ancestors before descendants, then ID. The walk also stops at twice as many links as generations (4 by default), so second cousins wait for "load more".
  - **Read side:** `IGraphQueries` in Application, `EfGraphQueries` in Infrastructure. The local view is one recursive CTE that keeps one row per (person, distance, generation), then the nearest `Graph:ViewPeople` people, plus one more row to tell it was cut short. Each person's `HasMore` is an `EXISTS` check for an active link to someone not in the view. The connection search walks breadth first with one row per person and stops at `Graph:PathSearchPeople` people (default 10,000), then loads the active links between them.
  - **Paths:** `RelationshipPaths` in Business is a pure k-shortest simple paths search (Yen's algorithm) over those links: the shortest path, then up to `Graph:MorePaths` (default 5) more, ordered by length and then by link IDs. One more is searched for, to tell that further paths exist. No path repeats a person, so a cycle is tolerated.
  - **Privacy:** `PrivacyProjection` in Application is the only way to create a `PersonReference`, which graph read models return. Query services return `PersonRecord`. Until #18 it passes every name through; #18 adds the `Private` and `Deleted` placeholders there.
  - **Endpoints:** `GET /api/people/{id}/graph` returns the centre, the people (each with its generation, `hasMore` and whether you maintain it), the active links between them with their type, and `isTruncated`. "Load more" around an edge person is the same request centred on them. `GET /api/people/{id}/connections/{otherId}` returns the paths, each step with its link, `Parent` or `Child` and the link type, plus `morePathsExist` and `searchComplete`. When the search is not complete, no path means "no connection found within the search limit". Unknown people are 404 `person.not_found`.
  - **Overview (agreed 2026-10-22, for the home page, D-024):** `GET /api/graph/overview` returns everyone reached over active links from the profiles you maintain, nearest first, with the active links between them and `isTruncated`, in one response. It is the connection search's breadth-first walk started from all your profiles at once, cut at `Graph:OverviewPeople` people; nobody is the centre, so there are no generations or `hasMore`. Without profiles it is empty.
  - **Timeout:** each graph query runs under `Graph:QueryTimeoutSeconds` (default 10) by cancelling it, which also stops it on the server. A timeout is 503 `graph.timeout`.
  - **Limits:** `Graph:ViewGenerations` (default 2, 1 to 4), `Graph:ViewPeople` (default 10, 1 to 500), `Graph:OverviewPeople` (default 500, 1 to 500), `Graph:PathSearchPeople` (default 10,000), `Graph:MorePaths` (default 5). A value out of range stops the Server at startup.
- **Consequences:** A view centred on an edge person returns people the client already shows, and its `hasMore` flags are relative to that response; #23 may add a "people already shown" filter if the client needs it. The search examines people nearest to the first person, so a longer alternative path may be missed near the limit. The existing relatives and inactive-links lists move to `PrivacyProjection` with #18. A seeded test with more than 10,000 people checks both queries stay well inside the timeout.

### D-024: Client graph view

- **Status:** proposed (2026-10-22). Issue #23. The library and the first slice were chosen by the user on 2026-10-22; the rest is proposed with the PR.
- **Context:** PRD 08 asks for a tree and a fan diagram centred on one person, "load more" from edge people, adoptive links drawn differently with a legend, and step-by-step path explanations. PRD 09 asks that relationships can be understood as text as well. Views are bounded by D-009 and D-023 (10 people by default, at most 500 per response). Issue #23 makes the rendering library a dependency decision.
- **Options considered:** hand-rolled SVG with no dependency; `d3-hierarchy` and `d3-shape` (trees only, a family graph has two parents per child); Cytoscape.js with the dagre layered layout; ELK.js (best layout, about 1.5 MB).
- **Trade-offs:** Cytoscape gives a layered layout, pan, zoom and selection with no layout code of our own, at the cost of about 140 kB gzipped in the lazy graph chunk and a canvas that keyboard and screen reader users cannot use. The text view covers them (AC-09.2), so the canvas is never the only way to inspect relationships.
- **Decision:**
  - **Dependencies (agreed):** `cytoscape` `^3.34.4` and `cytoscape-dagre` `^4.0.1` (both MIT; `cytoscape-dagre` 4 bundles dagre and ships its own types). They load only with the graph page's lazy route.
  - **First slice (agreed):** the tree diagram, the text view and the path view. The fan diagram is a follow-up.
  - **Route:** `/people/:id/graph`, linked from the person page as "Show the family graph". "Centre the view here" navigates to another person's graph.
  - **Home page (agreed 2026-10-22, revised the same day):** an overview of everyone connected through active links to the profiles you maintain, from one request to `GET /api/graph/overview` (D-023), at most `Graph:OverviewPeople` (500) people, the furthest left out. It draws small coloured dots and lines only: no legend, centre, picker or text below it; hovering a dot shows only the person's name. Nobody is the centre and nothing is remembered in the browser. Selecting a dot opens that person's graph page. The dots use the family layout (revised 2026-10-23 at the user's request): people start in rows by generation (brothers and sisters side by side below their parents, partners next to each other, more space between families than within one) and then settle off the rows: links pull their two people about a row apart, partners pull together and nearby people push apart, with children always at least half a row below each parent and nobody overlapping. Nothing is random, so the same family always looks the same. The picture groups like roads on a map rather than a symmetric tree. Keyboard and screen reader users get the same people as links to their graph pages, visible only while one has focus (AC-09.2). With no profiles, home asks you to add one.
  - **Diagram:** parents above children (dagre, top to bottom), one straight line per link so shared parents stay unambiguous. Adoptive links are dashed; people with relatives not shown have a double border; the centre has an accent border. A legend sits above the diagram. Colours come from the page's CSS custom properties. The canvas has `role="img"` and a summary that points to the list.
  - **Avatars (agreed 2026-10-22):** on the graph page every person has a coloured initial avatar (first letters of the first and last names; the colour is fixed per person ID, and home's dots use the same colours) beside their name, and hovering shows their name, generation, "Maintained by you" and "Has relatives not shown". Long names end in "…".
  - **Text view:** every shown person grouped by generation relative to the centre, oldest first, each with their shown parents and children as links, adoptive ones marked "(adoptive parent)" or "(adopted child)", and "Load more around …" and "Centre here" where they have relatives not shown.
  - **Load more:** the view centred on the edge person is merged into the shown people; new people get their generation shifted by the edge person's. A person keeps "has more" only while every merged view says so, and an edge person whose view added nobody loses it. The client keeps at most 500 people on screen and then asks to centre the view instead.
  - **Path view:** "How is … related to someone?" with the other person chosen from the people shown and the profiles you maintain until person search (#22). It lists the shortest path step by step ("parent", "child", "adoptive parent", "adopted child"), notes "There are more connections between these people", and shows the further paths on request. An incomplete search with no path says none was found within the search limit.
- **Consequences:** `Private` and `Deleted` placeholders render once #18 adds them to the API. The canvas is not covered by unit tests (jsdom has no canvas); the Playwright smoke test (D-006) should cover it. Highlighting a found path in the diagram is not part of this slice.

### D-025: Profile privacy and placeholders

- **Status:** agreed (2026-10-23). Issue #18, first part. Agreed with the user on 2026-10-23, including the ordering of hidden people and reading the administrator role from the session: two PRs (privacy settings and the `Private` placeholder first; profile deletion and the `Deleted` placeholder next); `GET /api/people/{id}` answers 200 with the placeholder rather than 404; a deleted profile leaves its former maintainer's list.
- **Context:** PRD 05 and PRD 10 settle the product rules: Public (default) or Private, set by the maintainer; a Private profile is a `Private` placeholder to everyone but its maintainer and administrators; link types show next to placeholders; active links through placeholders stay traversable; privacy changes are audited. D-010 and D-023 put one privacy projection in Application in front of every read path, and D-015 keeps names out of audit events.
- **Options considered:** projecting in each query (SQL) or once in Application; telling administrators apart by passing the session's role to Application or by looking the account up; how a name-sorted list orders placeholders; returning 404 or a placeholder for a hidden profile page.
- **Trade-offs:** One Application projection keeps the rule in one place and testable without a database; the queries still load the name, so a query that skips the projection would leak it, which the per-endpoint tests guard against. Sorting a hidden person by their real name would hint at it (an alphabetical position narrows the initial), so lists sort hidden people last, by ID. The role from the session cookie is current at sign-in (D-018), like every other role check.
- **Decision:**
  - **Business:** `Person.Visibility` is a `ProfileVisibility` (`Public` 1, `Private` 2), Public on creation. `Person.SetVisibility` is maintainer only (403 `person.not_maintainer`); setting the current value again is 409 `person.visibility_unchanged`. It records `PersonVisibilityChanged` with the new visibility and nothing about links or consent.
  - **Application:** `Viewer` (account ID and whether it is an administrator) is the reader on every read use case. Queries return `PersonRecord` (ID, name, visibility, whether the viewer maintains it); `PrivacyProjection.Project(record, viewer)` is the only producer of `PersonReference`, which the person page, relatives, inactive links, consent responses, local view, overview and connections all return. A hidden person keeps their ID and `Visibility`, has `Placeholder` `Private`, no name, and `MaintainedByViewer` false. The list of profiles you maintain is not projected: you always see your own profiles.
  - **Ordering:** relatives and inactive links put people hidden from the viewer after the others, by ID, with an empty name in the sort key (birth order still comes first for children).
  - **Persistence:** a `visibility` integer column on `people`; the `ProfileVisibility` migration sets existing rows to Public.
  - **API:** `PUT /api/people/{id}/visibility` with `{ "visibility": "Public" | "Private" }` (400 otherwise), 204. Every person in a response gains `placeholder` (`"Private"` or null) and `visibility`; the name parts are null for a placeholder. The maintained list gains `visibility`.
  - **Client:** a placeholder renders as "Private" with no initials and no name details. The person page offers "Make private" / "Make public" to the maintainer, and marks a Private profile they or an administrator see by name. "Profiles you maintain" marks private ones.
- **Consequences:** The second part of #18 adds deletion: a `Deleted` placeholder value, erasure of the name, `PersonDeleted` telling maintainer from administrator, no new links to a deleted profile, and leaving the maintained list. Audit records already hold no names (D-015), so erasure needs no rewrite of earlier records. Person search (#22) and derived relationships (#21) must return `PersonReference` through the same projection. An administrator's role change applies from their next sign-in.

### D-026: Profile deletion

- **Status:** agreed (2026-10-23). Issue #18, second part. Agreed with the user on 2026-10-23, including leaving deleted profiles out of their former maintainer's Approvals page: a deleted profile leaves its former maintainer's list; consent on a deleted profile's side of a link is frozen; a sole maintainer may still delete a link to a deleted profile; the erased name is stored as `[erased]`.
- **Context:** PRD 05 and PRD 10 settle the product rules: the maintainer, or an administrator for moderation, deletes; deletion erases personal data for good and leaves a `Deleted` placeholder; active links stay active and traversable; no new links to a deleted profile; the audit history tells maintainer from administrator and keeps only IDs, types and times. D-025 provides the projection and placeholder, and D-015 already keeps names out of audit events.
- **Options considered:** who answers consent for a deleted profile's pending links (nobody, or its former maintainer); whether a sole maintainer may delete a link once one of its profiles is deleted; erasing by overwriting the name with `[erased]` or by making the name columns nullable.
- **Trade-offs:** Freezing the deleted side means a pending link to a deleted profile never becomes active, but nobody keeps managing a profile that no longer exists; the other side's maintainer keeps their own approval to give or withdraw (PRD 03). Deleting a link changes the link, not the profile, and is the only way out for a sole maintainer (D-014). Overwriting keeps `PersonName` and the columns non-null at the cost of a marker value that reads never show.
- **Decision:**
  - **Business:** `Person.Delete(actor, actorIsAdministrator, at)` sets `DeletedAt`, replaces the name with `PersonName.Erased` (`[erased]`, no middle or last name) and records `PersonDeleted` with `ProfileDeleter` `Maintainer` or `Administrator` (the maintainer role wins when an administrator deletes their own profile). Anyone else is 403 `person.not_maintainer`. A deleted profile rejects rename, visibility and a second deletion with 409 `person.deleted`. `ParentChildLink.Create` rejects a deleted endpoint with 409 `link.person_deleted` (AC-05.12); approving or disagreeing for a deleted endpoint is 409 `person.deleted`; `Delete` and the other side's consent work as before.
  - **Application:** `PersonRecord.IsDeleted`; `PrivacyProjection` turns a deleted profile into the `Deleted` placeholder for everyone, administrators and its former maintainer included. `PersonService.DeleteAsync` takes the `Viewer` as actor. The maintained list leaves deleted profiles out, so the home overview no longer starts from them. The inactive-links list counts only non-deleted profiles you maintain, so a deleted profile's former maintainer is no longer asked to approve for it. Name-sorted lists put deleted people last with the hidden ones.
  - **Persistence:** a nullable `deleted_at` column on `people` (`ProfileDeletion` migration). Earlier audit records hold no names (D-015), so nothing in them needs replacing.
  - **API:** `DELETE /api/people/{id}`, 204. Placeholders in responses can be `"Deleted"`.
  - **Client:** "Delete profile" with an inline confirmation that says it cannot be undone, for the maintainer or an administrator; a deleted profile reads "Deleted" and offers no link form.
- **Consequences:** A pending link whose pending side is deleted stays inactive for good and remains on the other maintainer's Approvals page until they delete or keep it. Account deletion and succession (#41) must skip deleted profiles. Merging (#31) must decide how a deleted profile takes part.

### D-027: Voluntary profile handover

- **Status:** agreed (2026-10-23). Issue #76, merged in #77. The product rules (a shared link any signed-in user may accept, 14-day expiry, no handover of claimed profiles, consent unchanged) were agreed with the user on 2026-10-23 and are in PRD 01; the rest is proposed with the PR.
- **Context:** PRD 01 now lets a maintainer hand a profile over with a one-time link. D-018 keeps accounts to a Google ID and email, with no account search. D-015 keeps secrets and personal data out of audit events. Claims (#25) do not exist yet, so every profile is unclaimed for now.
- **Options considered:** storing the link's secret or only its hash; putting the secret in the URL path, a query string or the fragment; one open link per profile, or several; a separate `handover.accepted` event on the link or one profile event; how to stop two accounts accepting the same link at once.
- **Trade-offs:** A stored hash means a database leak gives no usable links, at the cost of showing the link only once. A fragment never reaches the server or its logs; the API takes the secret in the request body. One open link keeps "cancel" unambiguous, and a lost link is simply replaced. The maintainer change is the profile's event, so it sits with the profile's other events.
- **Decision:**
  - **Business:** `ProfileHandover` (`Business/Handovers`) with `HandoverState` (`Open` 1, `Accepted` 2, `Cancelled` 3; expiry is read from the time). `ProfileHandover.Offer` is maintainer only (403 `person.not_maintainer`, 409 `person.deleted`) and records `HandoverOffered` with the expiry; `Cancel` is maintainer only and records `HandoverCancelled`. `Person.AcceptHandover(handover, actor, now)` checks the link is open, unexpired and its creator still maintains the profile (409 `handover.closed` or `handover.expired`), refuses its own maintainer (409 `handover.own_profile`), changes `MaintainerId` and records `PersonHandedOver` with the previous maintainer and the handover ID. Links are not touched. The claimed-profile check (AC-01.17) is added with #25.
  - **Application:** `HandoverService` and `IHandoverStore`. The secret is 32 random bytes in URL-safe Base64 (`HandoverToken`); only its SHA-256 hash is stored and looked up. A new link cancels the open one in the same transaction. Expiry is `Handovers:LinkLifetimeDays` (default 14, 1 to 90) days, in whole seconds. The preview runs the profile through `PrivacyProjection`, so a Private profile stays a placeholder until accepted, and never names the maintaining account.
  - **Persistence:** `profile_handovers` (`ProfileHandovers` migration) with a unique token hash, at most one open row per person (filtered unique index; a race is 409 `handover.already_open`) and `state` as a concurrency token, so a second simultaneous accept is 409 `handover.closed`. Audit events of a handover have its person as subject.
  - **API:** `POST /api/people/{id}/handover` (201 with `token` and `expiresAt`), `GET` (200 with `createdAt` and `expiresAt`, or 204), `DELETE` (204), all maintainer only; `POST /api/handovers/preview` and `POST /api/handovers/accept` with `{ "token": … }` (400 without it; unknown is 404 `handover.not_found`).
  - **Client:** `features/handovers`. The person page shows "Hand over this profile" to its maintainer: create a link (shown once, with "Copy link"), see when the open one expires, create a new one or cancel it. The link opens `/handover#<secret>`, which shows the profile and "Accept the profile", then opens the person page.
- **Consequences:** Expired and used links stay stored for their history; nothing deletes them yet. #25 must refuse handover offers and acceptances for claimed profiles. #41 (succession) changes maintainers too, which voids open links automatically because their creator no longer maintains the profile.

### D-028: Profile images

- **Status:** agreed (2026-10-23). Issue #74, merged in #78. Agreed with the user on 2026-10-23: images are stored as a blob in PostgreSQL; the browser resizes and re-encodes, the server validates; images show on the person page and the graph page avatars, not the home dots. The rest is proposed with the PR.
- **Context:** PRD 01 and PRD 05 now give a profile an optional image that only its maintainer changes and that placeholders never show. D-002 keeps Business free of I/O, D-015 keeps personal data out of audit events, and no image library is a dependency.
- **Options considered:** re-encoding on the server with an image library, in the browser with canvas, or not at all; image bytes in a column of `people`, or a separate table; one row per profile, or one per uploaded image; a public cacheable image URL, or one that checks privacy on every request.
- **Trade-offs:** Canvas needs no dependency and strips metadata for every normal upload; a hand-crafted request can still store its own metadata, which only exposes the uploader's own data. A separate table keeps the blob out of every person load and query. A new row (and ID) per upload makes the ID a version for cache-busting URLs. Revalidating each image request costs a round trip, but a profile made Private stops showing its image at once.
- **Decision:**
  - **Business:** `ProfileImage` (ID, person ID, `ImageFormat`, bytes) and `ImageFormat` (`Jpeg` 1, `Png` 2, `WebP` 3, with their media types), detected from the file signature. `Person.SetImage(content, actor)` is maintainer only (403 `person.not_maintainer`, 409 `person.deleted`), checks the image (422 `profile_image.too_large` over 1 MiB, `profile_image.unsupported_format`), sets `Person.ImageId` and records `PersonImageChanged`. `RemoveImage` records `PersonImageRemoved` (409 `person.no_image` without one). `Delete` clears `ImageId`. The events hold IDs only.
  - **Application:** `IProfileImageStore`; `PersonService` adds the new image and removes the replaced one in the same transaction, and deletion removes the image row. `PersonRecord` and `PersonReference` carry `ImageId`, which `PrivacyProjection` drops for any placeholder. `GetImageAsync` projects the profile first, so a placeholder answers like a profile without an image (404 `profile_image.not_found`).
  - **Persistence:** `profile_images` (`bytea` content, format and size check constraints, foreign key to `people`) and a nullable `people.image_id` without a foreign key, so the two tables do not depend on each other (`ProfileImages` migration).
  - **API:** `PUT /api/people/{id}/image` with the raw image as the body (`image/jpeg`, `image/png` or `image/webp`, else 415; the body is read only up to the limit) answers `{ "imageId" }`; `DELETE` answers 204; `GET` answers the bytes with `Cache-Control: private, no-cache`, an ETag of the image ID and `nosniff`. People in person and graph responses gain `imageId`.
  - **Client:** the person page shows the image and offers "Add photo", "Change photo" and "Remove photo" to the maintainer. Before upload the browser crops the centre square, scales it to 512 px and re-encodes it as JPEG. The graph page diagram uses the image in place of the initials; the home dots do not change. Image URLs carry `?v=<imageId>`.
- **Consequences:** Data export leaves images out (D-029). Merging (#31) must decide which image the merged profile keeps.

### D-029: Data export

- **Status:** agreed (2026-10-23). Issue #75, merged in #79. Agreed with the user on 2026-10-23: only administrators export, and the export is the whole instance; it holds the full current state without images (every profile, Private ones with their names, deleted ones without; every non-deleted link with both approvals and birth order; the maintainers' emails), in one JSON file; each export is recorded in the audit history. Import is a later issue. The rest is proposed with the PR.
- **Context:** PRD 09 now asks for an export a later import can read back. D-018 keeps accounts to a Google ID and email, D-015 keeps personal data out of audit events, D-009 bounds every graph query, and D-010 puts API models in Server.
- **Options considered:** which accounts to include (all, or those that maintain a profile); a deleted profile's name as the stored `[erased]` marker or as null; building the file in memory or streaming it; reading each section in its own statement or from one database snapshot; a `POST` that answers a file, or a `GET` link the browser downloads.
- **Trade-offs:** Streaming keeps memory flat for any instance size but means an error after the first byte cuts the download short instead of answering a ProblemDetails. One repeatable-read, read-only transaction holds a PostgreSQL snapshot for the whole download, so every link's people and every person's maintainer are in the file, at the cost of one long read. A `GET` link needs no client code to save the file; a cross-site request could only make an administrator's own browser download it, but it would write an audit row.
- **Decision:**
  - **Business:** `DataExported` (actor only, `Administrative` family) and `NotAdministratorException` (403 `export.not_administrator`) in `Business/Export`.
  - **Application:** `ExportService.StartAsync(viewer)` refuses a non-administrator, records `DataExported` and saves it before opening the data, then returns a `DataExport` that streams accounts, people and links. `IExportQueries.OpenSnapshotAsync` returns an `IExportSnapshot` (disposable) whose sections stream in ID order. Accounts are those that maintain at least one profile, deleted or not. A deleted profile has no name. This is the only read without a node limit (D-009), because its purpose is the whole instance.
  - **Persistence:** `EfExportQueries` opens a repeatable-read, read-only transaction; accounts come from one SQL query over `user_accounts` and `people`; people and links are no-tracking queries. A `DataExported` audit row has the exporting account as its subject. No migration.
  - **API:** `GET /api/export` answers `application/json` as an attachment `familygraph-export-<yyyy-MM-dd>.json`, `Cache-Control: no-store`. The file is `{ "format": "familygraph-export", "version": 1, "exportedAt", "accounts": [{ "id", "email", "emailVerified" }], "people": [{ "id", "firstName", "middleName", "lastName", "visibility", "deletedAt", "maintainerId" }], "links": [{ "id", "parentId", "childId", "type", "parentApproval", "childApproval", "birthOrder" }] }`, with enumerations by name. It is written to the response pipe and flushed every 64 KiB. A layout change an import must know about raises `version`.
  - **Client:** the account page shows administrators a "Data export" section with a "Download data export" link to `/api/export`, a note that Private names are included and that the download is audited.
- **Consequences:** Marital relationships and anything else stored later must be added to the file, raising `version` if an import would misread the old layout. The import (later issue) must accept version 1, keep IDs, and decide how accounts are matched by email or Google ID; the export holds no Google ID. Audit history, deleted links and images are not exported, so an imported instance starts without them.

### D-030: Genders on profiles

- **Status:** agreed (2026-10-24). Issue #80, merged in #81. Agreed with the user on 2026-10-24: the values are Male, Female and Other, optional; the gender is seen wherever the name is and hidden like it; relationship wording is gendered where the gender is known (mother, father, daughter, son), parent and child otherwise. The rest is proposed with the PR.
- **Context:** PRD 00 requires only a name, PRD 05 keeps placeholders free of details, D-015 keeps personal data out of audit events, and marital relationships (#73) wait on genders.
- **Options considered:** a "not set" value or null; recording the gender in the audit event; computing the wording on the server or in the Client; refusing an unchanged gender with 409 like visibility, or accepting it like a rename.
- **Trade-offs:** Null keeps "not set" out of the value set, so nothing has to treat it as a gender. Wording in the Client keeps the API free of display text, and every response already carries each person's projected gender. Accepting an unchanged gender keeps the edit form simple; it writes one audit row.
- **Decision:**
  - **Business:** `Gender` (`BaseEnum`: `Male` 1, `Female` 2, `Other` 3). `Person.Gender` is nullable; `Person.Create(name, creator, gender)` takes an optional gender. `Person.SetGender(gender or null, actor)` is maintainer only (403 `person.not_maintainer`, 409 `person.deleted`) and records `PersonGenderChanged` with IDs only. `Delete` clears the gender.
  - **Application:** `PersonService.CreateAsync` takes the gender and `SetGenderAsync` changes it. `PersonRecord`, `PersonReference`, `PersonSummary` and `ExportedPerson` carry it; `PrivacyProjection` drops it for any placeholder.
  - **Persistence:** nullable `people.gender` integer (`PersonGender` migration), read by every person query including the graph's SQL.
  - **API:** `POST /api/people` accepts an optional `gender`; `PUT /api/people/{id}/gender` with `{ "gender": "Male" | "Female" | "Other" | null }` answers 204 (400 for any other value). Person, relative, link endpoint, graph and maintained-list responses gain `gender`, null when not set or for a placeholder. The export's people gain `gender`; `version` stays 1, since an import that ignores it reads the rest unchanged.
  - **Client:** the name form (add person page, new person dialog, person page "Edit details") has an optional gender select, "Not set" by default. The person page shows the gender when set. `relationWord` names a parent "mother", "father" or "parent" and a child "daughter", "son" or "child", with "adoptive" or "adopted" before it for adoptive links; path steps, the person page's parents and children, the graph page's text view and the Approvals page use it.
- **Consequences:** Marriages (#73) can name spouses "husband" or "wife" the same way. Merging (#31) must decide which gender the merged profile keeps. The planned admin label rules may depend on the labelled person's gender, falling back to a neutral rule when it is not known or hidden (PRD 08, agreed with the user on 2026-10-24).

### D-031: Marriages

- **Status:** agreed (2026-10-24, PR #82 merged). Issue #73. Agreed with the user on 2026-10-23 and 2026-10-24 (PRD 02, PRD 08): consent as for parent–child links; an ended marriage is marked former, by either spouse's maintainer without consent, and can be undone; any number per person, one per pair; any genders, with husband, wife or spouse wording; a full graph step within a generation, drawn with a distinct line; no marriage between a direct parent and child and no parent–child link between spouses. The rest is proposed with the PR.
- **Context:** PRD 00 limited direct facts to parent–child links, and the graph, paths, consent pages and export (D-020 to D-023, D-029) knew only links.
- **Options considered:** a marriage as a new link type on `ParentChildLink`, or its own aggregate and table; spouses stored as given, or in ID order; separate graph lists of links and marriages, or one list of edges; a new export `version` or the same.
- **Trade-offs:** Its own aggregate keeps the parent–child rules (cycles, birth order, types) away from a symmetric relationship. ID order gives each pair one row, so a unique index enforces one marriage per pair. Separate lists keep the existing link responses unchanged.
- **Decision:**
  - **Business:** `Marriage` (`Relationships`) with `FirstSpouseId` < `SecondSpouseId`, an `EndpointApproval` per side, `IsFormer` and `DeletedAt`; it is active when not deleted and both sides are approved. `Create`, `Approve`, `Disagree`, `Delete` and `SetFormer` follow `ParentChildLink`'s rules and record `MarriageCreated`, `MarriageSpouseApproved`, `MarriageApprovalRestored`, `MarriageSpouseDeclined`, `MarriageSpouseDisagreed`, `MarriageDeleted` (relationship consent family) and `MarriageFormerChanged` (profile family, like a birth-order change), with IDs only. Errors: `marriage.self`, `marriage.parent_child` (422), `marriage.not_spouse_maintainer`, `marriage.consent_not_maintainer`, `marriage.not_sole_maintainer` (403), `marriage.duplicate`, `marriage.person_deleted`, `marriage.consent_unchanged`, `marriage.former_unchanged`, `marriage.deleted` (409), `marriage.sole_maintainer_must_delete` (422). `ParentChildLink.Create` takes whether the pair is married and rejects it with `link.spouses` (422). The graph has `GraphEdge` with `GraphLink` and `GraphMarriage`; `RelationshipPaths` takes the active marriages and adds a `Spouse` step.
  - **Application:** `MarriageService` (create, approve, disagree, former, delete, a person's spouses, the viewer's inactive marriages), `IMarriageStore` and `IMarriageQueries`. Spouses and consent lists go through `PrivacyProjection`. `RelationshipService` checks for a marriage before creating a link. Local views, the overview and connections carry the active marriages between the people they return. The export streams `MarriagesAsync`.
  - **Persistence:** `marriages` table (`Marriages` migration) with a check that the first spouse's ID is lower, a unique index on the pair where not deleted (a race maps to `marriage.duplicate`) and partial indexes on each spouse over active rows. The graph's recursive CTEs follow active marriages at the same generation offset.
  - **API:** `POST /api/marriages` with `{ "spouseId", "otherSpouseId" }` answers 201; `POST /api/marriages/{id}/approve` and `/disagree` with `{ "spouseId" }`; `PUT /api/marriages/{id}/former` with `{ "isFormer" }` answers 204; `DELETE /api/marriages/{id}` answers 204; `GET /api/people/{id}/spouses`; `GET /api/marriages/inactive`. Graph responses gain `marriages` (`id`, `firstSpouseId`, `secondSpouseId`, `isFormer`); a connection step through a marriage has the type `Marriage` or `FormerMarriage`. The export gains `marriages` (`id`, `firstSpouseId`, `secondSpouseId`, `firstApproval`, `secondApproval`, `isFormer`); `version` stays 1, since an import that ignores the new array reads the rest unchanged.
  - **Client:** the person page lists spouses (husband, wife or spouse, "former" when ended) with "Mark as former"/"Mark as current", withdraw or delete, and an "Add a spouse" form. The Approvals page has a Marriages section. The graph page draws a marriage as a double accent line, dotted when former, with legend entries; the tree diagram keeps spouses on one row by laying out a hidden stand-in child of both (dagre cannot lay out an edge within a row). The text view lists spouses; the home dots treat spouses as partners in `family-layout.ts`.
- **Consequences:** Merging (#31) must also merge marriages, keeping one per pair. The planned admin label rules (PRD 08) can use marriages for in-law labels. An import must read `marriages`.

### D-032: Adding relatives from the graph, and person search

- **Status:** agreed (2026-10-24). Issue #85, merged in #86. Agreed with the user on 2026-10-24 (PRD 08): a right-click menu on the graph page's diagram only (Add parent, Add children, Add spouse), on everyone but deleted people; the list starts with your connections and typing searches every Public profile; on someone else's person it offers only your own profiles; picking works as on the person page, with "Add a new person". The rest is proposed with the PR.
- **Context:** Links and marriages could only be made from the person page, picking from the profiles you maintain (D-020), so two families kept by different accounts could only be joined by opening the other person's page. Person search (#22) did not exist; D-010 already fixed its shape (the top 20 matches and whether there are more).
- **Options considered:** your connections from the existing overview, or a new connections query; a substring match on each name part, prefix matches, or PostgreSQL full-text search; searching as you type on the server for your connections too, or filtering them in the browser; a new bulk endpoint for several links, or the existing one per link.
- **Trade-offs:** The overview (D-023, up to `Graph:OverviewPeople` people) already is "everyone connected to your profiles", so no new query is needed. A case-insensitive substring match finds "Rai" in "Maya Devi Rai" and needs no index or new dependency; it scans the people table, which is acceptable at the sizes PRD 09 names and can gain a trigram index later. Filtering connections in the browser answers at once, and the server is asked only for people beyond them. One request per link keeps each link's consent, cycle check and audit apart, as on the person page.
- **Decision:**
  - **Search:** `PersonService.SearchAsync` splits the text into words; every word must appear (ignoring case, `ILIKE` with `%` and `_` escaped) in the first, middle or last name. `IPersonQueries.SearchAsync` matches only non-deleted profiles that are Public or maintained by the viewer, so a Private profile is never found by its hidden name, even by an administrator; the results still go through `PrivacyProjection`. Sorted by name then ID, at most `People:SearchLimit` (default 20, 1 to 100), with one more row to say there are more. Blank text finds nobody.
  - **API:** `GET /api/people/search?q=…` answers `{ "people": [person…], "isTruncated" }` with the same person shape as `GET /api/people/{id}`; a query longer than a full name (302 characters) is 400.
  - **Client:** `GraphDiagram` takes `addMenu` (the graph page sets it; the home dots never) and opens a small menu on Cytoscape's `cxttap` (right click) and `taphold` (long press), suppressing the browser's own menu over the canvas; the first item takes the focus and Escape closes it. `AddRelatives` is a modal dialog: for a person you maintain it lists your profiles (`GET /api/people/maintained`) and the named people of the overview, filtered as you type, and from 2 characters, 300 ms after the last key, adds `GET /api/people/search` matches; for someone else's person it lists only your profiles. It ticks several parents or children (one spouse), with Biological or Adoptive, and "Add a new person" shows `PersonNameForm` in the dialog. It creates each link or marriage with the existing endpoints and reports pending ones. The graph view then merges a fresh local view around the person, so new active links appear.
  - **Accessibility:** the canvas is not keyboard accessible (D-024), and the person page keeps the same ways to add parents, children and spouses, which is the keyboard and screen reader route (AC-09.2).
- **Consequences:** #22 can reuse the search endpoint as an entry point into graph exploration. Searching by name lets any signed-in user find any Public profile by name, as Public already allowed by viewing; making a profile Private keeps it out of search.

### D-033: Data import

- **Status:** agreed (2026-10-24). Issue #87, merged in #91. Agreed with the user on 2026-10-24 (PRD 09): import reads the export file (D-029, version 1); only administrators import; only into an instance without any profile; a profile whose maintainer account does not exist here goes to the importing administrator; the import records one audit event and nothing per row; a file with any problem is refused whole. The rest is proposed with the PR.
- **Context:** D-029 left the import to a later issue: it must accept version 1, keep IDs, and match accounts without a Google ID. An account is only a Google ID and an email (D-018), emails are not unique (`RequireUniqueEmail` is off) and an email can be unverified. The domain's creation methods give the creator's side approval and record events, which an import of stored states cannot use.
- **Options considered:** matching a file account by any account with its email, or only by exactly one verified one; reading the file as it streams, or whole; validating in Server, or in Application with every problem listed; a transaction that only checks emptiness, or one that locks the tables; a multipart upload, or the raw JSON body.
- **Trade-offs:** Matching only a single verified account means an unverified or shared email can never hand profiles to the wrong person; the importing administrator can hand them over later (D-027). Reading the file whole is simple and keeps the checks over all rows (cycles, duplicate pairs) in memory; the 64 MiB cap holds far more than PRD 09's 10,000 people. Locking the people, link and marriage tables for the import lets reads go on and makes a second, concurrent import wait and then find the instance not empty.
- **Decision:**
  - **Business:** `Person.Import`, `ParentChildLink.Import` and `Marriage.Import` restore an entity with its ID and stored states and record no event; a deleted profile keeps only the erased name. `LinkCycleCheck.HasCycle` checks a whole set of links. `DataImported` (actor only, `Administrative` family; subject the importing account) and `ImportNotAdministratorException` (403 `import.not_administrator`) in `Business/Import`.
  - **Application:** `ImportService.ImportAsync(viewer, data)` refuses a non-administrator, opens `IImportStore.BeginAsync`, refuses an instance with any profile (409 `import.instance_not_empty`), checks every row in file order (unique IDs, enumerations by name, the name rules and the 100-character limit of D-019, links and marriages between people in the file, no duplicate pair, no cycle, no marriage of a parent and child, birth order at least 1) and refuses the file with `InvalidImportFileException` (422 `import.invalid_file`, the first 50 problems with `path`, `code` and `message`, and `moreProblems`). Then it matches each file account's email with `FindAccountsByVerifiedEmailAsync`, adds everything through the existing stores, records `DataImported` and commits. Rows that reference an unknown maintainer ID also go to the importer.
  - **Persistence:** `EfImportStore` takes `LOCK TABLE people, parent_child_links, marriages IN SHARE ROW EXCLUSIVE MODE` in its transaction and matches emails on Identity's normalized email. No migration.
  - **API:** `POST /api/import` with the export file as the `application/json` body, at most 64 MiB (413 beyond). Unreadable JSON is `import.invalid_json`, another `format` `import.unsupported_format`, another `version` `import.unsupported_version`, each as one problem of a 422. Unknown fields are ignored and missing sections are empty. It answers `{ "people", "links", "marriages", "matchedAccounts", "assignedToYou" }`.
  - **Client:** the account page shows administrators a "Data import" box under the export: a file picker and "Import file", then the counts, or the refusal with its problems.
- **Consequences:** An imported instance starts with one audit entry and no per-row history (PRD 07 traceability begins at the import). A maintainer who signs in after the import does not get their profiles automatically; the administrator hands them over. A later export version must stay importable or raise `version` with a matching import.

### D-034: Deleting profiles from the graph

- **Status:** agreed (2026-10-25). Issue #92, merged in #93. Agreed with the user on 2026-10-24 (PRD 08): the graph page's right-click menu offers Delete profile to the maintainer or an administrator, under the deletion rules of D-026, after a confirmation dialog. The rest is proposed with the PR.
- **Context:** Profiles could only be deleted from the person page (D-026). The diagram's menu (D-032) already has the person, and each graph person carries `maintainedByYou`; the Client knows whether you are an administrator from `GET /api/account`.
- **Options considered:** a new endpoint, or the existing `DELETE /api/people/{id}`; reloading the whole view after a deletion, or replacing the one person in it.
- **Trade-offs:** The existing endpoint already enforces who may delete and records the audit event, so the Server and the domain stay as they are. Replacing the one person with the placeholder fetched from `GET /api/people/{id}` keeps the people the viewer loaded with "load more", which a reload of the centre's view would drop; links stay as they were, because deletion leaves them active.
- **Decision:**
  - **Client:** `GraphDiagram` takes `administrator` and, on a person you maintain or on anyone as an administrator, adds **Delete profile** to the menu (never on a deleted person, who has no menu) and emits `deletePerson`. `GraphView` opens `DeletePerson`, a modal dialog that names the person, says the name, photo and gender are erased for good and that they stay as "Deleted" with their links, and calls `DELETE /api/people/{id}`; a refusal shows in the dialog. Then the person in the view is replaced by `GET /api/people/{id}` (the `Deleted` placeholder) and a notice says so.
  - **Server, Application, Business:** no change; the menu is only a hint, and the server decides (403 `person.not_maintainer`, 409 `person.deleted`).
  - **Accessibility:** the person page's "Delete profile" stays the keyboard and screen reader route (AC-09.2).
- **Consequences:** Every later menu action follows the same pattern: the Client hides what you may not do, and the existing endpoint enforces it.

### D-035: Administrator Users page and profile transfers

- **Status:** agreed (2026-10-25). Issue #94, merged in #95. Agreed with the user on 2026-10-25 (PRD 01): an administrator moves one profile or all of an account's profiles to another account without its approval; the Users page shows each account's email, verified and administrator flags, number of maintained profiles and an expandable list of those profiles. The rest is proposed with the PR.
- **Context:** Maintainers change only through voluntary handover (D-027), which the receiver accepts. An account is a Google ID, an email and a verified flag (D-018); administrators come from configuration (D-008) and already see maintainers' emails in the export (D-029).
- **Options considered:** a new aggregate or a method on `Person`; one event for a bulk transfer or one per profile; loading the profiles of a bulk transfer or one set-based `UPDATE`; including deleted profiles in a bulk transfer.
- **Trade-offs:** A method on `Person` keeps the rule with the profile's other maintainer changes and records its event in the profile's history. One event per profile keeps every profile's history complete at the cost of more rows; loading the profiles lets each check and record its own transfer, and an account maintains far fewer profiles than PRD 09's 10,000. Deleted profiles accept no change (D-026) and leave the maintained list, so a bulk transfer skips them.
- **Decision:**
  - **Business:** `Person.Transfer(newMaintainerId, actorId, actorIsAdministrator)` refuses a non-administrator (403 `transfer.not_administrator`), a deleted profile (409 `person.deleted`) and the current maintainer (409 `transfer.same_maintainer`), changes `MaintainerId` and records `PersonTransferred` (Profile family) with the previous and new maintainer IDs. Links are not touched. The claimed-profile check (AC-01.28) is added with #25.
  - **Application:** `Accounts/AccountAdministrationService` checks the viewer is an administrator first (403 `accounts.not_administrator` for the lists, `transfer.not_administrator` for transfers), then: `ListAccountsAsync` through `IAccountQueries` (up to `Accounts:ListLimit`, default 1000, with `isTruncated`), `ListProfilesAsync` through `IPersonQueries.ListMaintainedByAsync`, `TransferProfileAsync`, and `TransferAllAsync`, which loads the non-deleted profiles with the new `IPersonStore.ListMaintainedByAsync` and saves them with their events in one transaction (409 `transfer.same_account` to itself). An unknown account is 404 `account.not_found`.
  - **Persistence:** `Identity/EfAccountQueries` reads accounts with the administrator role and a live count of non-deleted maintained profiles in one query, sorted by email and ID. No schema change.
  - **API:** `GET /api/admin/accounts`, `GET /api/admin/accounts/{id}/profiles`, `POST /api/admin/accounts/{id}/transfer` and `POST /api/admin/people/{id}/transfer`, both with `{ "toAccountId" }` (400 without it); the first answers `{ "transferred" }`, the second 204.
  - **Client:** `features/admin`. The header shows **Users** to administrators; `/admin/users` (behind `adminGuard`) lists the accounts, opens one to list its profiles with links, and offers a per-profile transfer and "Transfer all profiles" after a confirmation. The person page shows administrators "Transfer to another account", which loads the accounts when opened.
- **Consequences:** #25 must refuse transfers of claimed profiles. Account deletion (PRD 01) can reuse `Person.Transfer` when it moves profiles to an administrator. The Users page has no search; a much larger instance would need one before `Accounts:ListLimit` is reached.

### D-036: Linking spouses too, and choosing relatives on a new profile

- **Status:** agreed (2026-10-25). Issue #88 (with #89), PR #96. Agreed with the user on 2026-10-25 (PRD 01, PRD 08): adding children to a person offers each of their spouses as the other parent, and adding a parent offers that parent's spouses as parents; every spouse through an active marriage has a checkbox, ticked when the marriage is current and unticked when former; the extra links copy the type you picked; the Add a person page, and only that page, lets you choose parents and children. The rest is proposed with the PR.
- **Context:** Links were made one pair at a time, so a couple's child took two adds, and a new profile had to be linked from its person page afterwards. The Client already has each person's spouses (`GET /api/people/{id}/spouses`, active marriages only, with `isFormer` and `maintainedByYou`), the person search and connections of D-032, and creates links one request at a time.
- **Options considered:** a server endpoint that creates a profile with its links, or several links, in one transaction; the existing endpoints one link at a time from the Client; a separate picker on each page, or one shared component.
- **Trade-offs:** One request per link keeps each link's permission, cycle check, consent and audit apart, as D-032 does, and needs no Server change; the cost is that a failure leaves the other links (and the new profile) in place, so the Client reports each link's outcome. A shared picker keeps the graph dialog and the Add a person page searching the same way.
- **Decision:**
  - **Client:** `features/relationships/PersonPicker` is the list and search of D-032, moved out of `AddRelatives`, which now uses it. `SpouseLinkChoices` loads the spouses of the person (adding children) or of each picked parent (adding parents) and lists one checkbox each, "Sita Rai, wife of Hari Rai", with current spouses ticked. It leaves out deleted spouses (no new links), the person and the people already picked, and a spouse where you maintain neither side of any resulting link (PRD 03). After the picked people's own links, the ticked spouses are linked to the people whose link was created, with the same type; `link.duplicate` counts as "already linked". The graph's add-relatives dialog and the person page's add-links form show it. The Add a person page has optional **Choose parents** and **Choose children** sections (a `PersonPicker` that searches everyone, a type each, and the spouse choices for parents) inside `PersonNameForm`, which now projects content above its buttons. It creates the profile, then the parents' links, then their spouses', then the children's, and opens the new profile when every link is active, or stays to list each outcome with a link to the profile.
  - **Server, Application, Business:** no change.
- **Consequences:** Nothing here is derived: a child is never linked to a spouse unless the box was left ticked. A later bulk-link endpoint could replace the per-link requests without changing the rules.

### D-037: Descriptions, birth dates and setting birth order

- **Status:** agreed (2026-10-25). Issue #97, PR #100. Agreed with the user on 2026-10-25 (PRD 01, PRD 02, PRD 05): birth order stays a number on each parent–child link (D-016), with no `SiblingOrder` on the person; the parent's maintainer moves children up or down on the person page, which numbers the list 1, 2, 3…; a profile may have a one-line description of at most 200 characters, private like the name; a profile may have a birth date, a year with an optional month and day, stored as entered in BS or AD, with a "show to others" switch that is off by default (only the maintainer and administrators see a date that is not shown); unordered children will sort by birth dates shown to others, then by name (#99); the Client date picker is `@opensourcenepal/nepali-calendar-angular` and the server converts with `OSN.NepaliCalendar`. This supersedes D-016's "no birth dates".
- **Context:** D-016 put birth order on the link but nothing could set it. Profiles held only a name and gender, so two people with the same name looked alike. Families in Nepal often know a birth date in Bikram Sambat, and for older relatives only a year; a BS year starts in mid-April, so a partial BS date is not one AD year. Business references nothing (D-002), so it cannot convert BS dates.
- **Options considered:** a `SiblingOrder` on the person, or the link's number; storing dates always in AD, always in BS, or as entered; full dates only, or partial ones; converting BS in Business with a copied table, in Application with the package, or in the Client; one endpoint per field, or one for all details; setting birth order one link at a time, or a whole list.
- **Trade-offs:** The per-link number keeps half siblings ordered under each parent. Storing as entered keeps exactly what the family knows; a sort date is still needed, so Application converts with the package (one table shared with the Client) and the Client is not trusted for it. One endpoint per field matches gender and visibility and keeps each audit event small. Numbering the whole list in one request keeps a move to one transaction, where per-link requests could leave two children on the same number.
- **Decision:**
  - **Business:** `BirthDate` (`Calendar` `BirthCalendar.Bs` or `Ad`, `Year`, optional `Month`, optional `Day` that needs a month, and `SortDate`, the earliest AD day it can mean). `BirthDate.Ad` checks that the day exists and computes the sort date; `BirthDate.Bs` checks ranges (day up to 32) and takes the sort date from the caller. `PersonDescription.Normalize` trims, makes blank null, and rejects more than 200 characters (`person.description_too_long`) or a line break (`person.description_not_one_line`). `Person` has `Description`, `BirthDate` and `BirthDateShown` (false by default); `Create` and `Import` take them; maintainer-only `SetDescription` records `PersonDescriptionChanged` and `SetBirthDate(date, shown)` records `PersonBirthDateChanged` with the switch only (D-015); `Delete` erases all three. An invalid date is `person.birth_date_invalid` (422).
  - **Application:** `BirthDates.Create` builds a date and converts BS to AD with `OSN.NepaliCalendar` 1.0.0 (MIT, no dependencies) for BS 1970 to 2090; outside those years it checks ranges and estimates the sort date as 13 April of the year 57 earlier plus the month and day. `PersonService.CreateAsync` takes `PersonDetails`; `SetDescriptionAsync` and `SetBirthDateAsync`. `PersonRecord` and `PersonReference` carry the description, date and switch; `PrivacyProjection` drops all three for placeholders and returns the date only when it is shown, or to its maintainer or an administrator. `RelationshipService.OrderChildrenAsync(parent, linkIds)` checks that every link is the parent's and listed once, then numbers them 1, 2, 3… with `SetBirthOrder` for each changed one, in one transaction; links not listed keep their numbers. Export and import carry `description`, `birthDate` (`calendar`, `year`, `month`, `day`) and `birthDateShown`; files without them still import, so the version stays 1.
  - **Infrastructure:** `people` gets `description` (with `ck_people_description_length`), `birth_calendar`, `birth_year`, `birth_month`, `birth_day`, `birth_sort_date` (an optional complex property) and `birth_date_shown`; migration `PersonDescriptionAndBirthDate`. The graph's person query reads them too.
  - **Server:** `POST /api/people` takes `description`, `birthDate` and `birthDateShown`; `PUT /api/people/{id}/description` and `PUT /api/people/{id}/birth-date` (`birthDate`, `shown`) answer 204; person and search responses carry `description`, `birthDate` and `birthDateShown`. `PUT /api/people/{id}/children/order` (`linkIds`) answers 204, 403 `link.birth_order_not_parent_maintainer`, 404 `link.not_found` for a link that is not the parent's, 422 `link.birth_order_invalid` for a repeated one.
  - **Client:** `PersonNameForm` has a description field and `BirthDateInput` (BS or AD, year, month and day selects, "Pick on the calendar" with `osn-nepali-calendar` where the table reaches, "Remove birth date") with the "Show the birth date to others" box. `formatBirthDate` shows a full date in both calendars and a partial one in its own. The person page shows the description under the name and a "Born" line, with "(hidden from others)" when the date is not shown. The children list on the person page has up and down arrows for the parent's maintainer. New npm dependencies (agreed): `@opensourcenepal/nepali-calendar-angular` `^1.2.0` and its core `@opensourcenepal/nepali-calendar` `^1.2.0` (both MIT, by the project's maintainer), whose stylesheet is imported in `styles.css`.
- **Consequences:** Relatives, graph and approval responses do not carry the new fields yet; the People page and picker (#98) and graph ordering by shown birth dates (#99) build on these.

### D-038: One people picker, and avatars and descriptions in lists

- **Status:** agreed (2026-10-25). Issue #98, PR #101. Agreed with the user on 2026-10-25 (PRD 01, PRD 08): the People page and the people pickers show each person's photo or initials and description, as list rows; one shared picker is used everywhere people are picked, the person page's spouse form included, and every one offers "Add a new person". Amended 2026-10-25 (the user asked): the People page has a search box.
- **Context:** The graph dialog and the Add a person page used `PersonPicker` (D-032, D-036), but the person page had its own checkbox list for parents and children, with its own new-person dialog, and a radio list of your profiles for a spouse with no way to add a new person. The picker's rows and the People page showed names only, so people with the same name looked alike. The list of your profiles (`GET /api/people/maintained`) carried no image or description, and graph responses no description.
- **Options considered:** the picker asks its host to create the new person (as before), or creates it itself; the person page keeps lists of your profiles only, or uses the picker with the graph's rules; a nested form on the Add a person page, or no "Add a new person" there.
- **Trade-offs:** A picker that creates the person itself removes three copies of the dialog. Using the graph's rules on the person page lets a person you maintain be linked to anyone Public there too; the server already enforces PRD 03 the same way for both. A `<form>` built by the DOM may contain another one; the dialog stops its form's `submit` and `reset` events so they never reach the outer form.
- **Decision:**
  - **Application, Infrastructure:** `PersonSummary` gains `ImageId` and `Description`, read by `ListMaintainedByAsync`. No new endpoint or migration.
  - **Server:** maintained-list items carry `imageId` and `description`; graph people carry `description` (null for a placeholder).
  - **Client:** `features/people/PersonAvatar` (`app-person-avatar`) shows the photo through `imageUrl`, or the initials, in a 2.5 rem circle; a placeholder shows an empty circle. `PersonPicker` rows show the avatar, the name with "(yours)" and the description; `Candidate` gains `imageId` and `description`. "Add a new person" is on by default (`offersNewPerson`) and opens the picker's own dialog with `PersonNameForm` (every optional detail), creates the profile with `PeopleService.create`, lists and selects it and emits `created`. `AddRelatives`, `PersonRelatives`, `PersonSpouses` and `NewPersonPage` use it; `PersonRelatives` and `PersonSpouses` search everyone when you maintain the person (`searchesEveryone`), and only your profiles otherwise. The People page is a list of rows (`.person-rows`, `.person-row`) with the avatar, the name, "(private)" and the description, and a search box that filters the loaded list in the browser (every word in a name part or the description). The list is capped at `People:MaintainedListLimit` (500), which the page already says when reached, so no server search was added.
- **Consequences:** No server rule changed. The home dots stay dots (D-028). Graph ordering by birth order (#99) is next.

### D-039: Children in birth order in the graphs

- **Status:** agreed (2026-10-25). Issue #99, PR #104. Agreed with the user on 2026-10-25 (PRD 02, PRD 08): children are ordered by the parent's birth order, then the unordered ones by birth dates shown to others, then by name; a hidden date never orders anyone, even for its maintainer, so everyone sees one order; this holds in the tree diagram, the text view and the home dots (their starting places). The rest is proposed with the PR.
- **Context:** Birth order lives on each link (D-016) and a birth date on the person (D-037), but graph responses carried neither, the person page's children ignored birth dates, and the diagrams placed people in the order the walk found them. dagre orders each row to reduce crossings, so brothers and sisters with the same parents, who tie, can come out in any order.
- **Options considered:** sending the birth date itself, or only a sort key; sorting on the server, or in the Client per view; for the tree, insertion order alone, or moving siblings after dagre.
- **Trade-offs:** A sort key (the earliest day the date can mean) is all the ordering needs and is null whenever the date is not shown, so the graph never carries a date the person page would hide. The Client already merges views around edge people (AC-08.5), so only it can order what is on screen. Insertion order alone did not survive dagre's crossing reduction in a browser check, so the tree moves siblings afterwards.
- **Decision:**
  - **Business:** `GraphLink` gains an optional `BirthOrder`, display only and never traversed.
  - **Infrastructure:** the graph's link query reads `birth_order`. The person page's children sort by link birth order, then by the birth sort date when shown and not hidden from the viewer, then as before (hidden last, by name).
  - **Server:** graph links carry `birthOrder`; graph people carry `birthSortDate` (`yyyy-mm-dd`), set only when the date is shown to others (never for a placeholder).
  - **Client:** `features/graph/child-order.ts` has `compareChildren` (one parent's children), `siblingSets` and `orderSiblings` (each set of brothers and sisters with the same shown parents in order, kept in the places the set had; two siblings compare by birth order under each shared parent in turn) and `orderRow`. The text view lists each person's children and each generation's siblings in order. The home dots get `orderSiblings` as their starting order. The tree adds people and links in order, then `orderRow` lays each set of siblings, each with the spouses standing next to them, out again eldest on the left over the same stretch of the row, keeping the gaps; siblings with someone else between them stay where dagre put them.
- **Consequences:** Half siblings are ordered within each set of parents, not across sets. No new endpoint, migration or dependency.

### D-040: Administrators approving pending sides on behalf of maintainers

- **Status:** agreed (2026-10-25). Issue #105, PR #106. Agreed with the user on 2026-10-25 (PRD 03): an administrator approves only a pending side, on behalf of whoever maintains it, with no "unavailable" check; declining and restoring stay with the maintainer; administrators find these on the Approvals page, in a section listing every link and marriage waiting for another maintainer. The rest is proposed with the PR.
- **Context:** A link or marriage made with someone else's profile stays inactive until that maintainer approves (PRD 03), and an absent maintainer leaves it stuck. PRD 03 said administrators never override consent (AC-03.5); merges already let an administrator stand in for an unavailable maintainer of an unclaimed profile (PRD 06). Claims (#25) are not built, so no profile is claimed yet.
- **Options considered:** a flag on `Approve`, or its own method; reusing `LinkEndpointApproved` with the administrator as actor, or its own event; one instance-wide list, or a filter on the inactive-links query.
- **Trade-offs:** Its own method leaves the maintainer check of `Approve` untouched and keeps the pending-only rule in one place. Its own event, with the maintainer's ID, lets history tell an administrator's approval from the maintainer's (AC-03.8) without looking up account roles afterwards. A separate query leaves the viewer's own list (D-021) as it is; the new one is bounded by `Relationships:InactiveLinksLimit` like it.
- **Decision:**
  - **Business:** `ParentChildLink.ApproveAsAdministrator(endpoint, actorId, actorIsAdministrator, parentAncestors)` and `Marriage.ApproveAsAdministrator(spouse, actorId, actorIsAdministrator)` refuse a non-administrator (403 `consent.not_administrator`), a deleted link, marriage or profile (409), an approved side (409 `link.consent_unchanged` or `marriage.consent_unchanged`) and a declined or withdrawn side (409 `consent.not_pending`). The link re-runs the cycle check when it would become active. They record `LinkEndpointApprovedByAdministrator` or `MarriageSpouseApprovedByAdministrator` (RelationshipConsent family) with the endpoint, the administrator and the maintainer's account ID. The claimed-profile check (AC-03.10) is added with #25.
  - **Application:** `RelationshipService` and `MarriageService` get `ApproveAsAdministratorAsync` and `ListPendingForAdministratorAsync`; each refuses a non-administrator before loading anything. `IRelationshipQueries.ListPendingLinksAsync` and `IMarriageQueries.ListPendingMarriagesAsync` return non-deleted rows with a pending side whose person is not deleted and is maintained by someone other than the viewer, by name, cut at the limit with `isTruncated`.
  - **Infrastructure:** both queries in `EfRelationshipQueries` and `EfMarriageQueries`. No schema change.
  - **Server:** `GET /api/relationships/pending`, `POST /api/relationships/{id}/approve-as-administrator` (`{ "endpoint" }`), `GET /api/marriages/pending` and `POST /api/marriages/{id}/approve-as-administrator` (`{ "spouseId" }`), answering like the existing consent endpoints.
  - **Client:** the Approvals page loads the two lists for administrators and shows **Waiting for other maintainers**, with "Approve for <name> as administrator" on each pending side someone else maintains.
- **Consequences:** #25 must refuse administrator approval for claimed profiles. Maintainers are not told; there are no notifications yet.

### D-041: Enlarging the profile photo

- **Status:** agreed (2026-10-25, PR #108 merged). Issue #107. Asked by the user on 2026-10-25 (PRD 01): clicking the photo on the person page opens it enlarged, with a button to close it. The rest is proposed with the PR.
- **Context:** The person page shows the photo as a 10rem circle, though the stored photo is a 512 px square (D-028).
- **Options considered:** a native modal `<dialog>`, or a hand-made overlay; a new route, or an overlay on the page.
- **Trade-offs:** `showModal()` gives the backdrop, Escape, a focus trap and focus return without a dependency or extra code, as the graph dialogs already use it (D-034). A route would reload the page for a picture.
- **Decision:**
  - **Client:** in `PersonPhoto` the photo is a button ("Enlarge photo of <name>") that opens a modal dialog with the same image square, up to 32rem and scaled to fit the screen. Its Close button, Escape or a click on the backdrop close it.
- **Consequences:** No server change, endpoint or dependency. The graph page's avatars do not enlarge.

### D-042: All people on the People page, paged

- **Status:** proposed (2026-10-25). Issue #22. Agreed with the user on 2026-10-25 (PRD 08, PRD 05): the People page gets an All people tab next to My profiles, listing everyone you may see by name A to Z, 50 at a time with Show more, with a search box; administrators also see Private profiles, marked "(private)"; nobody sees deleted profiles. The rest is proposed with the PR.
- **Context:** The People page listed only the profiles you maintain, and the person search of D-032 returns the top 20 Public matches for pickers. #22 asks for a bounded, paginated search as an entry point into the graph, and an instance can hold more than 10,000 people (PRD 09). D-010 left lists unpaginated until a real list needed it.
- **Options considered:** search only with the top 20 (D-032); everything up to a limit; paging by offset or by a cursor on the name; one endpoint for search and browsing, or a second one.
- **Trade-offs:** Paging is what lets the list hold everyone at any size; this is the first list that needs it, so D-010 is amended for it alone. An offset is simpler than a cursor over three nullable name parts; a profile created or deleted between pages can shift a row by one, which the Client absorbs by skipping IDs it already has. A second endpoint leaves the picker search (names only, top 20) unchanged, while browsing also matches descriptions as the My profiles box does (PRD 01).
- **Decision:**
  - **Application:** `PersonService.BrowseAsync(text, offset, viewer)` returns `PeoplePage` (`People`, `HasMore`), every row run through `PrivacyProjection` and placeholders dropped. `IPersonQueries.BrowseAsync(terms, viewerId, includePrivate, offset, limit)` lists non-deleted profiles that are Public, the viewer's, or any when `includePrivate` (the viewer is an administrator), each word matching a name part or the description with `ILIKE`, by first, middle and last name and ID. `PeopleLimits.PageSize` (`People:PageSize`, 1 to 100, default 50).
  - **Infrastructure:** `EfPersonQueries.BrowseAsync`, `Skip` and `Take` one row more than a page. No schema change.
  - **Server:** `GET /api/people?q=&offset=` answers `{ people, hasMore }` with person responses; 400 for a negative offset or a query over 302 characters.
  - **Client:** the People page has My profiles and All people tabs (`?tab=all` keeps the choice in the URL). All people loads the first page, Show more asks for the next with the loaded count as the offset, and the search box asks the server again after a short pause. Rows are the D-038 `.person-rows` with "(yours)" and "(private)".
- **Consequences:** D-010 lists stay unpaginated except this one. A much larger instance may want a cursor or a name index later; the picker search (D-032) is unchanged.
