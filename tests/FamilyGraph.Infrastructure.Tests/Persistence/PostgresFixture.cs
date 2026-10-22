using FamilyGraph.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(FamilyGraph.Infrastructure.Tests.Persistence.PostgresFixture))]

namespace FamilyGraph.Infrastructure.Tests.Persistence;

/// <summary>
/// One disposable PostgreSQL server for the test run (D-009). Each test gets its own empty
/// database, so tests never see each other's rows.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine").Build();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Options for a new database that does not exist yet, with no migrations applied.</summary>
    internal DbContextOptions<FamilyGraphDbContext> NewEmptyDatabase() => OptionsFor(NewConnectionString());

    /// <summary>The connection string of a new database with every migration applied.</summary>
    internal async Task<string> NewMigratedConnectionStringAsync()
    {
        var connectionString = NewConnectionString();
        await using var context = new FamilyGraphDbContext(OptionsFor(connectionString));
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        return connectionString;
    }

    private string NewConnectionString() =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = "test_" + Guid.NewGuid().ToString("N"),
        }.ConnectionString;

    private static DbContextOptions<FamilyGraphDbContext> OptionsFor(string connectionString)
    {
        var options = new DbContextOptionsBuilder<FamilyGraphDbContext>();
        options.UseFamilyGraphNpgsql(connectionString);
        return options.Options;
    }

    /// <summary>Options for a new database with every migration applied.</summary>
    internal async Task<DbContextOptions<FamilyGraphDbContext>> NewMigratedDatabaseAsync() =>
        OptionsFor(await NewMigratedConnectionStringAsync());
}
