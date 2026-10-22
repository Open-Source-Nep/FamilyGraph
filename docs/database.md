# Database

FamilyGraph stores everything in one PostgreSQL database through EF Core (D-008, D-009). The schema is created and changed only by EF Core migrations in `src/FamilyGraph.Infrastructure/Persistence/Migrations`.

## Configuration

The Server reads the connection string `FamilyGraph`. It is never committed (D-012).

| Where | How to set it |
|---|---|
| Self-hosted instance | Environment variable `ConnectionStrings__FamilyGraph`, e.g. `Host=db;Database=familygraph;Username=familygraph;Password=...` |
| Local development | `dotnet user-secrets set ConnectionStrings:FamilyGraph "<connection string>" --project src/Server` |

The Server starts without a connection string, but any request that uses the database fails until one is set.

Self-hosted instances run migrations as a separate step before the app starts; the app never migrates at startup (D-011). The Docker image, compose file and migration bundle come with #33.

## Local development

1. Start PostgreSQL (any PostgreSQL 18 works; Docker is the quickest):

   ```bash
   docker run -d --name familygraph-db -p 5432:5432 \
     -e POSTGRES_USER=familygraph -e POSTGRES_PASSWORD=familygraph -e POSTGRES_DB=familygraph \
     postgres:18-alpine
   ```

2. Point the Server at it:

   ```bash
   dotnet user-secrets set ConnectionStrings:FamilyGraph \
     "Host=localhost;Database=familygraph;Username=familygraph;Password=familygraph" --project src/Server
   ```

3. Apply the migrations (`dotnet tool restore` installs the pinned `dotnet-ef` once):

   ```bash
   dotnet tool restore
   dotnet ef database update --project src/FamilyGraph.Infrastructure \
     --connection "Host=localhost;Database=familygraph;Username=familygraph;Password=familygraph"
   ```

## Changing the schema

1. Change the entity configuration in `src/FamilyGraph.Infrastructure/Persistence/Configurations`.
2. Add a migration and review the generated code:

   ```bash
   dotnet ef migrations add <Name> --project src/FamilyGraph.Infrastructure --output-dir Persistence/Migrations
   ```

3. Run `dotnet test`. A test fails if the model has changes that no migration covers.

Tables and columns use lower snake_case, so hand-written SQL needs no quoted identifiers.

## Tests

`FamilyGraph.Infrastructure.Tests` starts a disposable PostgreSQL container with Testcontainers, so Docker must be running. Each test gets its own empty database.
