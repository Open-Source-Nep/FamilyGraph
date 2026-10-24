# Database

FamilyGraph stores everything in one PostgreSQL database through EF Core (D-008, D-009). The schema is created and changed only by EF Core migrations in `src/FamilyGraph.Infrastructure/Persistence/Migrations`.

## Configuration

The Server builds its connection from the `Database` section. Each key is an Npgsql connection string keyword (`Host`, `Port`, `Database`, `Username`, `Password`, `SSL Mode`, ...); an unknown key stops the Server at startup. The password is never committed (D-012).

| Where | How to set it |
|---|---|
| Self-hosted instance | Environment variables `Database__Host=db`, `Database__Database=familygraph`, `Database__Username=familygraph`, `Database__Password=...` |
| Local development | Copy `src/Server/appsettings.Development.dist.json` to `appsettings.Development.json` in the same folder (Git ignores it) and adjust it, or set values with `dotnet user-secrets set Database:Password "<password>" --project src/Server` |

The Server starts without `Database:Host`, but any request that uses the database fails until it is set.

Self-hosted instances run migrations as a separate step before the app starts; the app never migrates at startup (D-011). The image's migration bundle is described in `docs/deployment.md`.

## Local development

1. Start PostgreSQL (any PostgreSQL 18 works; Docker is the quickest):

   ```bash
   docker run -d --name familygraph-db -p 5432:5432 \
     -e POSTGRES_USER=familygraph -e POSTGRES_PASSWORD=familygraph -e POSTGRES_DB=familygraph \
     postgres:18-alpine
   ```

2. Point the Server at it:

   ```bash
   dotnet user-secrets set Database:Password familygraph --project src/Server
   ```

3. Apply the migrations (`dotnet tool restore` installs the pinned `dotnet-ef` once):

   ```bash
   dotnet tool restore
   dotnet ef database update --project src/FamilyGraph.Infrastructure --startup-project src/Server
   ```

   `dotnet ef` builds the Server's host in the Development environment, so it connects with the same `Database` settings and user secrets as the app. To target another database, add `--connection "<connection string>"` or set `Database__*` environment variables.

## Changing the schema

1. Change the entity configuration in `src/FamilyGraph.Infrastructure/Persistence/Configurations`.
2. Add a migration and review the generated code:

   ```bash
   dotnet ef migrations add <Name> --project src/FamilyGraph.Infrastructure --startup-project src/Server --output-dir Persistence/Migrations
   ```

3. Run `dotnet test`. A test fails if the model has changes that no migration covers.

Tables and columns use lower snake_case, so hand-written SQL needs no quoted identifiers.

## Tests

`FamilyGraph.Infrastructure.Tests` starts a disposable PostgreSQL container with Testcontainers, so Docker must be running. Each test gets its own empty database.
