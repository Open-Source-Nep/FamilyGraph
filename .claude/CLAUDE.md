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

- **People are not accounts.** A `Person` can exist without a `UserAccount`. One account can maintain many profiles. A new person needs only a name.
- **One maintainer per profile.** Before a claim, the contributor is the maintainer. After an approved claim, the claimant is.
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

Template scaffolding with no domain code yet. Layout and toolchain are agreed (D-001 in `docs/trd/open-decisions.md`):

- Solution `FamilyGraph.slnx`, all projects under `src/`. The Angular project folder is `src/client` (`client.esproj`).
- `global.json` pins the .NET SDK 10.0.1xx. `.nvmrc` selects Node 24.
- Client uses **Angular 22** with TypeScript 6.0, Vitest, and Prettier. Styling is plain CSS: no Tailwind or UI component library.
- `Server` targets `net10.0` with only `Microsoft.AspNetCore.OpenApi`. `Program.cs` is still the weatherforecast template. The SPA Proxy is **not** configured yet.
- No project references between the class libraries yet. Each contains only a placeholder `Class1.cs`.
- No tests, `deploy/`, database provider, or EF Core packages yet.

Open technical decisions, in order (see `docs/trd/open-decisions.md`): identity approach, database and graph query patterns (PostgreSQL is provisional), API conventions, testing strategy, production hosting, CI/CD and configuration.

## Commands

```bash
# Backend (from repo root)
dotnet build FamilyGraph.slnx
dotnet run --project src/Server            # http://localhost:5039, https://localhost:7027

# Frontend (from src/client)
npm install
npm start                                  # ng serve
npm run build
npm test                                   # Vitest via ng test
```
