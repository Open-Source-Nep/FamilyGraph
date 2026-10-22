# FamilyGraph

Open-source, self-hostable, community-maintained family graph. Priorities: privacy and minimal personal data, per-endpoint relationship consent, auditable history, and bounded graph exploration.

## Source of truth

- **Product behavior:** `docs/prd/` (start with `00-product-vision-and-working-agreement.md`). `docs/prd/family-graph-prd-pack.zip` is an archived copy of the same pack; edit the `.md` files, not the zip.
- **Technical direction:** `docs/trd/` (start with `README.md`).
- **Unresolved items:** `docs/prd/10-open-decisions.md` (product) and `docs/trd/open-decisions.md` (technical).

Read the relevant PRD/TRD files before proposing behavior or design. These docs win over assumptions; keep them up to date as decisions are agreed.

## Working agreement

- **Do not write application code until the technical specification is sufficiently agreed.** The current phase is requirements and architecture discussion. Implement only when the user explicitly authorizes a scoped task.
- Never invent product rules to fill PRD gaps. If work depends on an open decision: explain why it matters, offer a few options with consequences, and ask.
- Work one decision at a time. Ask focused questions instead of stacking major assumptions.
- Keep agreed, proposed, and unresolved decisions clearly separated. Do not silently change a settled decision. If a contradiction forces a revisit, explain it and ask first.
- When a decision is agreed, update the relevant TRD/PRD and record it in `open-decisions.md` (or a future ADR) using the decision-record convention there.
- Propose domain concepts and invariants before tables or code.
- Do not add dependencies (NuGet or npm) without the user's agreement. Explain purpose, alternatives, and maintenance cost first. Excluded by default: MediatR, AutoMapper, generic repository frameworks, global frontend state libraries, UI component libraries, graph databases, message brokers, microservices.
- Keep changes scoped to the approved task. Once implementation is authorized, add or update tests for the relevant acceptance criteria.
- Keep docs concise and do not duplicate PRD content into TRDs.

## Core product invariants (summary, see PRDs for detail)

- **People are not accounts.** A `Person` can exist without a `UserAccount`. One account can maintain many profiles. A new person needs only a first name (middle and last are optional).
- **One maintainer per profile.** Before a claim, the contributor is the maintainer. After an approved claim, the claimant is. After the maintainer's account is deleted, one administrator account inherits it.
- **Direct facts are parent–child only.** The only types are biological and adoptive. Siblings, grandparents, cousins, and other relationships are *derived* from active links and are never stored as authoritative facts.
- **Consent is tracked per endpoint**, not as a single `Active` flag. When both endpoints are claimed, both must approve. A claimed person's disagreement deactivates the link immediately. Withdrawing the disagreement reactivates it if the other approval is still valid.
- **Inactive links** are excluded from the active graph, traversal, and derivation, but stay in audit history.
- **Removal:** if one user maintains both endpoints, they may delete the link (an audit record is kept). If different users maintain them, the only option is withdrawing approval, and the audit history must tell these two cases apart.
- **No one overrides consent.** Not administrators, not maintainers, not merges, not claims. Claiming a profile does not change relationship consent state.
- **Identity claims and relationship consent are separate processes**, and their audit records must be distinct.
- **Privacy hides identity, not structure.** Private and deleted profiles render as `Private` or `Deleted` placeholders that leak no name or details. Active links through them stay traversable.
- **Adoption** must stay distinguishable along paths and be flagged in derived-relationship explanations.
- **No automatic merges.** Merges require review, never rely on name matches alone, preserve history, and never silently transfer a claimed profile or activate links that lack consent.
- **Scale and accessibility:** support more than 10,000 people without rendering the whole graph. Offer local/subgraph views and text explanations alongside visualization.

## Architecture (agreed baseline)

Modular monolith: ASP.NET Core backend, Angular SPA, one relational database.

| Project | Responsibility |
|---|---|
| `Server` | HTTP entry point, middleware, endpoints, auth middleware, DI composition root |
| `FamilyGraph.Application` | Use cases, orchestration, user management, auth/permission workflows, validation |
| `FamilyGraph.Business` | Domain entities, value objects, domain services, invariants (consent and relationship rules) |
| `FamilyGraph.Infrastructure` | EF Core `DbContext` (possibly `IdentityDbContext`-derived), migrations, persistence and identity implementations |
| `Client` | Angular SPA, organized by feature, with lazy routes and standalone components |

Rules:
- References (D-002): Server → Application, Infrastructure. Application → Business. Infrastructure → Business, plus Application only to implement Application-owned interfaces. **Business references nothing.**
- Application owns all store and query interfaces. Business defines none, and its domain services are pure. Writes go through focused per-aggregate stores plus `IUnitOfWork`, with audit events in the same transaction. Reads go through query services that return read models and take required depth and node limits. Derivation is a pure Business service over a bounded set of active links.
- Business must not depend on ASP.NET Core, HTTP types, EF Core, Angular, or Infrastructure.
- Never expose `DbContext` or EF entities as API contracts.
- Identity framework types are not the domain model. Keep user accounts separate from person profiles.
- Enforce privacy, consent, and permissions **server-side** on every read and write path, including traversal and derived queries. The Client is never authoritative.
- Graph queries must be bounded and must never load the whole graph.
- Don't create a repository per entity by default. Add a store interface only when a use case needs it.
- The Angular SPA Proxy is for local development only, not production hosting.

## Current state of the repo

Foundation wired (#8). Domain core in progress: `FamilyGraph.Business/People` has `Person`, `PersonName`, `PersonId`, `AccountId` (#12). `FamilyGraph.Business/Relationships` has `ParentChildLink` creation with per-endpoint `Approved`/`Pending` approval and the bounded `LinkCycleCheck` over an Application-supplied `AncestorSnapshot` (#14); consent transitions `Approve`/`Disagree` with a `Disagreed` state (#15, D-013); sole-maintainer `Delete` with a deletion timestamp (#16, D-014); entities record audit events with no personal data (`FamilyGraph.Business/Audit`, #10, D-015); domain errors derive from `DomainException` (D-007), and closed value sets derive from `BaseEnum<T>` instead of C# enums. Layout and toolchain are agreed (D-001 in `docs/trd/open-decisions.md`):

- Solution `FamilyGraph.slnx`, production projects under `src/`, test projects under `tests/` (D-004). The Angular project folder is `src/client` (`client.esproj`).
- `global.json` pins the .NET SDK 10.0.1xx and selects the Microsoft.Testing.Platform test runner. `.nvmrc` selects Node 24.
- Client uses **Angular 22** with TypeScript 6.0, Vitest, and Prettier. Styling is plain CSS: no Tailwind or UI component library.
- Project references follow D-002. Application contains no code yet.
- `Server` has `Microsoft.AspNetCore.OpenApi` and `Microsoft.AspNetCore.SpaProxy`. `Program.cs` only maps OpenAPI in Development. The SPA Proxy is enabled only via `launchSettings.json`: `dotnet run` starts `npm start` and redirects to `http://localhost:4200`. `src/client/proxy.conf.json` forwards `/api` and `/signin-google` to `http://localhost:5039`; HTTPS redirection runs outside Development only.
- Tests: xUnit v3 with built-in `Assert` (D-003), tagged with PRD `AC` IDs (D-005). `FamilyGraph.ArchitectureTests` enforces D-002. `Server.IntegrationTests` uses `WebApplicationFactory`. `FamilyGraph.Business.Tests` has the domain tests; `FamilyGraph.Infrastructure.Tests` has the persistence tests (Docker required); `FamilyGraph.Application.Tests` has the use-case tests with in-memory fakes.
- CI (#34, D-012): `.github/workflows/ci.yml` runs on PRs and pushes to `main`. Backend job: restore, build, `dotnet format --verify-no-changes`, `dotnet test`. Client job: `npm ci`, `prettier --check .`, `npm test`, `npm run build`. The release workflow waits for #33.
- Persistence (#9, D-017): `FamilyGraph.Infrastructure/Persistence` has the internal `FamilyGraphDbContext` (an `IdentityDbContext` since #11), fluent mappings of the domain entities, the `audit_events` table and the `InitialSchema` migration. Server calls `AddInfrastructure` with its configuration; the connection comes from the `Database` section (Npgsql keywords as keys). `FamilyGraph.Infrastructure.Tests` runs against Testcontainers PostgreSQL. Setup: `docs/database.md`.
- Accounts (#11, D-018): Google sign-in, first sign-in registers, account stores only Google ID + email + verified flag. `Application/Accounts` has `AccountService` and `IAccountStore`; `Infrastructure/Identity` implements it with `UserManager`. `Server/Accounts` has `AccountController` (`/api/account`), the session cookie and the antiforgery filter. Client has `core/auth` (service, guard) and `features/account` (sign-in, account pages). Setup: `docs/authentication.md`.
- No `deploy/` yet.

Agreed since: identity (D-008: Google sign-in, ASP.NET Core Identity, config-defined admins) and database (D-009: PostgreSQL + EF Core, Testcontainers, recursive CTEs with limits). API conventions (D-010: MVC controllers, API models in Server, ProblemDetails with error codes, graph expansion instead of pagination). Hosting (D-011: Server serves the Client, cookie sessions, Docker image + compose, migration bundle). CI/CD (D-012: GitHub Actions, env-var configuration, tagged releases to GHCR). No open technical decisions remain in `docs/trd/open-decisions.md`.

## Commands

```bash
# Backend (from repo root)
dotnet build FamilyGraph.slnx
dotnet run --project src/Server            # http://localhost:5039, https://localhost:7027; starts the SPA via the SPA Proxy
dotnet test                                # all .NET test projects (Microsoft.Testing.Platform)
dotnet test --project tests/FamilyGraph.ArchitectureTests
dotnet ef migrations add <Name> --project src/FamilyGraph.Infrastructure --output-dir Persistence/Migrations   # after dotnet tool restore

# Frontend (from src/client)
npm install
npm start                                  # ng serve
npm run build
npm test                                   # Vitest via ng test
```
