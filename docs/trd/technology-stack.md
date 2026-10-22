# Technology Stack

## Status

Agreed. Pinned versions are listed below and in the decision records.

## Stack

| Area | Choice | Status / notes |
|---|---|---|
| Backend runtime | .NET 10 LTS / ASP.NET Core | Agreed; SDK pinned in `global.json` (D-001) |
| Frontend | Angular 22, standalone components | Agreed (D-001); TypeScript 6.0, Node 24 LTS (`.nvmrc`) |
| Styling | Plain CSS (component-scoped styles + CSS custom properties) | Agreed (D-001); no Tailwind or UI component library |
| Backend language | C# | Agreed |
| Frontend language | TypeScript | Agreed |
| API style | REST with JSON, MVC controllers, ProblemDetails errors | Agreed (D-010) |
| Relational database | PostgreSQL (Npgsql provider) | Agreed (D-009) |
| ORM and migrations | Entity Framework Core; recursive CTEs for traversal | Agreed (D-009) |
| Identity persistence | ASP.NET Core Identity with EF Core, Google sign-in | Agreed (D-008, D-018). One shared `IdentityDbContext`-derived context; the Client runs the Google flow and the Server validates the ID token |
| Development integration | ASP.NET Core Angular SPA Proxy | Configured (#8); enabled only through `launchSettings.json` |
| Testing | xUnit v3 (built-in `Assert`), Microsoft.Testing.Platform; Vitest; Playwright smoke tests | Agreed (D-003–D-006); integration tests use Testcontainers PostgreSQL (D-009) |
| Packaging | One Docker image (Server + built Client), `deploy/docker-compose.yml` with PostgreSQL and a migration bundle step | Agreed (D-011) |
| Repository | One Git repository / monorepo | Agreed (D-001) |

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

Agreed later:
- EF Core provider: Npgsql (D-009). Versions pinned in #9 (D-017): `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3, `Microsoft.EntityFrameworkCore.Design` 10.0.4, `dotnet-ef` 10.0.4 (local tool in `.config/dotnet-tools.json`), `Testcontainers.PostgreSql` 4.16.0.
- Identity packages pinned in #11 (D-018): `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.4. Google ID tokens are validated with `Google.Apis.Auth` 1.77.0 (D-018, revised 2026-10-22); `Microsoft.AspNetCore.Authentication.Google` was removed.
- CI restore/build/test commands (D-012).

Do not assume that template defaults are current or that all package major versions are mutually compatible when upgrading.
