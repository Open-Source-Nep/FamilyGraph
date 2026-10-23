# Project References and Persistence Boundaries

## Project references (agreed, D-002)

```text
Server
 ├──> FamilyGraph.Application
 └──> FamilyGraph.Infrastructure

FamilyGraph.Application
 └──> FamilyGraph.Business

FamilyGraph.Infrastructure
 ├──> FamilyGraph.Business
 └──> FamilyGraph.Application  (only to implement Application-owned interfaces)

FamilyGraph.Business
 └──> no project references to Application, Infrastructure, or Server

Client
 └──> communicates with Server through HTTP APIs
```

Infrastructure references Application only to implement Application-owned interfaces; do not place application logic in Infrastructure. An architecture test enforces the graph (#8).

## Persistence boundary (agreed, D-002)

- **Business** owns domain types, invariants, and pure domain services. It defines **no** persistence or query interfaces. Domain services operate on objects passed to them.
- **Application** owns every persistence and query interface, shaped by its use cases.
- **Infrastructure** implements them with EF Core (or SQL where needed). EF types never leave Infrastructure.
- **Server** is the composition root that registers the implementations.

### Write side

- Use focused store interfaces per aggregate, created only when a use case needs them (e.g. `IPersonStore`, `IParentChildLinkStore`).
- One `IUnitOfWork.SaveChangesAsync` commits a use case's changes and its audit events in the same transaction.
- No generic repository and no repository per entity by default.

### Read side

- Use dedicated, Application-owned query services that return flat read models, not domain entities (e.g. `IPersonQueries`, `IRelationshipQueries`).
- Depth and node limits are **required parameters**, so an unbounded query cannot be expressed. The limit values follow D-009.
- Privacy projection (`Private`/`Deleted` placeholders) is applied in Application before results leave the use case (`PrivacyProjection`, D-025).

### Derived relationships

- Query services fetch a bounded set of **active** links.
- A pure Business service derives relationships, path explanations, and adoption indicators from that set.
- SQL fetches data; it never encodes what a relationship label means.

## Identity boundary

- Application owns user-management workflows, permission management, and application-level authorization decisions.
- Infrastructure may use ASP.NET Core Identity persistence and an `IdentityDbContext`-derived EF Core context.
- Server configures authentication middleware and endpoint authorization.
- Identity framework types should not automatically become the FamilyGraph domain model.
- Keep user accounts separate from person profiles, as required by the product.
- Sign-in method, roles and admin assignment are agreed in D-008 (`open-decisions.md`). Sessions are Identity cookies (D-011); the account model and sign-in flow are in D-018.

## Rules

- Business must not reference Infrastructure.
- Business must not depend on HTTP request types or EF Core.
- Do not expose `DbContext` or EF Core entities directly as API contracts.
- Enforce privacy and relationship-consent rules in server-side application/domain paths, not only in Angular.
