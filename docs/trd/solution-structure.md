# Solution Structure

## Agreed naming

- Solution: `FamilyGraph`
- API project: `Server`
- Angular project: `Client`
- Class libraries: `FamilyGraph.Business`, `FamilyGraph.Application`, `FamilyGraph.Infrastructure`

## Proposed repository layout

```text
FamilyGraph/
├── FamilyGraph.sln
├── global.json
├── README.md
├── Server/
│   └── Server.csproj
├── Client/
│   ├── package.json
│   ├── angular.json
│   └── src/
├── FamilyGraph.Business/
│   └── FamilyGraph.Business.csproj
├── FamilyGraph.Application/
│   └── FamilyGraph.Application.csproj
├── FamilyGraph.Infrastructure/
│   └── FamilyGraph.Infrastructure.csproj
├── tests/
│   ├── FamilyGraph.Business.Tests/
│   ├── FamilyGraph.Application.Tests/
│   ├── FamilyGraph.Infrastructure.Tests/
│   └── Server.Tests/
├── docs/
└── deploy/
```

This is a proposed initial layout, not an instruction to create every test project or folder before it is useful. Confirm how the selected Microsoft Angular template lays out its generated files, then adapt it while preserving the agreed project names.

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
