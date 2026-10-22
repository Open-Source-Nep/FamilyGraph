using FamilyGraph.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyGraph.Infrastructure.Tests.Persistence;

public class MigrationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Migrations_apply_cleanly_to_an_empty_database()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = new FamilyGraphDbContext(postgres.NewEmptyDatabase());

        await context.Database.MigrateAsync(cancellationToken);

        Assert.Equal(context.Database.GetMigrations(), await context.Database.GetAppliedMigrationsAsync(cancellationToken));
        Assert.Empty(await context.Database.GetPendingMigrationsAsync(cancellationToken));
    }

    [Fact]
    public async Task Model_has_no_changes_missing_from_the_migrations()
    {
        await using var context = new FamilyGraphDbContext(postgres.NewEmptyDatabase());

        Assert.False(context.Database.HasPendingModelChanges(), "Run 'dotnet ef migrations add' for the model changes.");
    }
}
