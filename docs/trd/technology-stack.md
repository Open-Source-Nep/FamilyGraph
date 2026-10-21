# Technology Stack

## Status

Proposed baseline; versions and supporting dependencies must be verified before project creation.

## Proposed stack

| Area | Proposed direction | Status / notes |
|---|---|---|
| Backend runtime | .NET 10 LTS / ASP.NET Core | Recommended; verify current SDK and template availability before setup |
| Frontend | Angular 22, standalone components | Recommended; verify Angular's current Node.js and TypeScript compatibility |
| Backend language | C# | Intended |
| Frontend language | TypeScript | Intended |
| API style | REST with JSON | Recommended baseline |
| Relational database | PostgreSQL | Provisional; finalize during data design |
| ORM and migrations | Entity Framework Core | Recommended baseline |
| Identity persistence | ASP.NET Core Identity with EF Core, if suitable | Infrastructure may use `IdentityDbContext`; exact identity approach remains to be specified |
| Development integration | ASP.NET Core Angular SPA Proxy | Intended |
| Packaging | Docker | Intended for self-hosting |
| Repository | One Git repository / monorepo | Recommended and reflected in proposed layout |

## Dependency policy

- Prefer framework capabilities and a small dependency set.
- Do not add MediatR, AutoMapper, a generic repository framework, global frontend state management, a UI component library, or a graph database by default.
- Explain a dependency's purpose, alternatives, and maintenance cost before proposing it.
- Do not add dependencies without the user's agreement.

## Versioning and reproducibility

Before implementation, agree and record:
- Exact .NET SDK version or feature band in `global.json`.
- Exact target framework.
- Exact Angular CLI and Angular package versions.
- Compatible Node.js and TypeScript versions.
- EF Core provider and package versions.
- Package-lock and restore/build commands used in CI.

Do not assume that template defaults are current or that all package major versions are mutually compatible.
