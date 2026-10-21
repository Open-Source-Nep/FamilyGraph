# Solution Structure

## Agreed naming

- Solution: `FamilyGraph`
- API project: `Server`
- Angular project: `Client` (folder `src/client`, project file `client.esproj`)
- Class libraries: `FamilyGraph.Business`, `FamilyGraph.Application`, `FamilyGraph.Infrastructure`

## Repository layout (agreed, see D-001 in `open-decisions.md`)

```text
FamilyGraph/
├── FamilyGraph.slnx
├── global.json
├── .nvmrc
├── README.md
├── src/
│   ├── Server/
│   ├── client/                     # Angular SPA ("Client")
│   ├── FamilyGraph.Business/
│   ├── FamilyGraph.Application/
│   └── FamilyGraph.Infrastructure/
├── tests/                          # added per the testing decision
├── docs/
└── deploy/                         # added per the hosting decision
```

Add `tests/` and `deploy/` folders only when they are needed.

## Project responsibilities

### `Server`
- ASP.NET Core application entry point.
- HTTP endpoints/controllers or endpoint groups.
- Middleware and API configuration.
- Authentication middleware and endpoint authorization configuration.
- Dependency-injection composition root.
- Request/response transport concerns.

### `FamilyGraph.Application`
- Application use cases and orchestration.
- User management and authentication/authorization workflows.
- Permission management and application-level authorization decisions.
- Request validation and use-case coordination.
- Abstractions for infrastructure services needed by application workflows, where appropriate.

### `FamilyGraph.Business`
- Domain entities, value objects, domain services, and business invariants.
- Family relationship and consent rules defined by the PRDs.
- Must not depend on ASP.NET Core, Angular, EF Core, or Infrastructure.

### `FamilyGraph.Infrastructure`
- EF Core `DbContext` and entity configurations.
- EF Core migrations.
- Persistence and query implementations.
- Identity persistence, including an `IdentityDbContext`-derived context if selected.
- Implementations of persistence and integration abstractions.

### `Client`
- Angular SPA.
- Feature-based organization, routes, UI components, API integration, and frontend state.
- No authoritative enforcement of privacy, consent, or permissions; the server remains responsible.

## Angular organization proposal

Organize by feature rather than only by technical type. Candidate feature areas include `people`, `family-graph`, `profile-claims`, `relationships`, `account`, and `administration`. These are organizational suggestions, not final product module boundaries.

Use standalone components, lazy-loaded feature routes, strict TypeScript settings, and framework-native patterns where suitable. Avoid adding a global state library or UI component library until a concrete need is established.
