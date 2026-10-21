# Technology Stack

## Status

Proposed baseline; versions and supporting dependencies must be verified before project creation.

## Proposed stack

| Area | Proposed direction | Status / notes |
|---|---|---|
| Backend runtime | .NET 10 LTS / ASP.NET Core | Agreed; SDK pinned in `global.json` (D-001) |
| Frontend | Angular 22, standalone components | Agreed (D-001); TypeScript 6.0, Node 24 LTS (`.nvmrc`) |
| Styling | Plain CSS (component-scoped styles + CSS custom properties) | Agreed (D-001); no Tailwind or UI component library |
| Backend language | C# | Intended |
| Frontend language | TypeScript | Intended |
| API style | REST with JSON, MVC controllers, ProblemDetails errors | Agreed (D-010) |
| Relational database | PostgreSQL (Npgsql provider) | Agreed (D-009) |
| ORM and migrations | Entity Framework Core; recursive CTEs for traversal | Agreed (D-009) |
| Identity persistence | ASP.NET Core Identity with EF Core, Google sign-in | Agreed (D-008). One shared `IdentityDbContext`-derived context |
| Development integration | ASP.NET Core Angular SPA Proxy | Configured (#8); enabled only through `launchSettings.json` |
| Testing | xUnit v3 (built-in `Assert`), Microsoft.Testing.Platform; Vitest; Playwright smoke tests | Agreed (D-003–D-006); integration tests use Testcontainers PostgreSQL (D-009) |
| Packaging | Docker | Intended for self-hosting |
| Repository | One Git repository / monorepo | Recommended and reflected in proposed layout |

## Dependency policy

- Prefer framework capabilities and a small dependency set.
- Do not add MediatR, AutoMapper, a generic repository framework, global frontend state management, a UI component library, or a graph database by default.
- Explain a dependency's purpose, alternatives, and maintenance cost before proposing it.
- Do not add dependencies without the user's agreement.

## Versioning and reproducibility

Agreed (D-001):
- `global.json` pins the .NET SDK feature band `10.0.1xx` (`rollForward: latestPatch`). Target framework `net10.0`.
- Angular CLI and packages `^22.2`, TypeScript `~6.0`.
- `.nvmrc` selects Node 24 LTS; `package.json` `engines` allows `^22.22.3 || ^24.15.0` (Angular 22's supported range).
- `package-lock.json` is committed; CI uses `npm ci`.
- `global.json` selects the Microsoft.Testing.Platform runner for `dotnet test`. xunit.v3 4.x does not support VSTest on the .NET 10 SDK.

Still to agree:
- EF Core provider and package versions.
- Restore/build commands used in CI.

Do not assume that template defaults are current or that all package major versions are mutually compatible.
