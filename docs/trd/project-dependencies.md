# Project References and Persistence Boundaries

## Proposed project references

```text
Server
 ├──> FamilyGraph.Application
 └──> FamilyGraph.Infrastructure

FamilyGraph.Application
 └──> FamilyGraph.Business

FamilyGraph.Infrastructure
 ├──> FamilyGraph.Business
 └──> FamilyGraph.Application  (only if needed to implement Application-owned abstractions)

FamilyGraph.Business
 └──> no project references to Application, Infrastructure, or Server

Client
 └──> communicates with Server through HTTP APIs
```

The exact reference graph should be kept as small as practical. If Infrastructure references Application, keep that dependency for implementing infrastructure-facing abstractions only; do not place application logic in Infrastructure.

## Persistence boundary

The preferred direction is:
- Business owns domain rules and domain types.
- Application coordinates use cases and may own interfaces that exist specifically to support those use cases.
- Business may own repository abstractions when they express domain-oriented persistence needs.
- Infrastructure implements persistence abstractions and contains EF Core details.
- Server is the composition root that registers concrete implementations.

Avoid creating repositories automatically for every entity. Decide per use case whether a repository abstraction, a focused query abstraction, or direct use of a suitable application-facing persistence abstraction is justified.

## Identity boundary

- Application owns user-management workflows, permission management, and application-level authorization decisions.
- Infrastructure may use ASP.NET Core Identity persistence and an `IdentityDbContext`-derived EF Core context.
- Server configures authentication middleware and endpoint authorization.
- Identity framework types should not automatically become the FamilyGraph domain model.
- Keep user accounts separate from person profiles, as required by the product.
- The exact authentication provider, login methods, token/cookie approach, and identity model remain unresolved.

## Rules

- Business must not reference Infrastructure.
- Business must not depend on HTTP request types or EF Core.
- Do not expose `DbContext` or EF Core entities directly as API contracts.
- Enforce privacy and relationship-consent rules in server-side application/domain paths, not only in Angular.
