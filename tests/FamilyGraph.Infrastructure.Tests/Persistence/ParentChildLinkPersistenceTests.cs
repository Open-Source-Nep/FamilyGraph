using FamilyGraph.Business.Relationships;
using FamilyGraph.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FamilyGraph.Infrastructure.Tests.Persistence;

public class ParentChildLinkPersistenceTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Link_round_trips_type_approvals_birth_order_and_deletion()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var options = await postgres.NewMigratedDatabaseAsync();
        var account = TestData.NewAccount();
        var other = TestData.NewAccount();
        var parent = TestData.NewPerson(account, "Parent");
        var child = TestData.NewPerson(other, "Child");
        var link = TestData.NewLink(parent, child, account);
        link.SetBirthOrder(parent, account, 2);

        await using (var context = new FamilyGraphDbContext(options))
        {
            context.AddRange(parent, child, link);
            await context.SaveChangesAsync(cancellationToken);
        }

        await using (var context = new FamilyGraphDbContext(options))
        {
            var loaded = await context.Set<ParentChildLink>().SingleAsync(l => l.Id == link.Id, cancellationToken);

            Assert.Equal(parent.Id, loaded.ParentId);
            Assert.Equal(child.Id, loaded.ChildId);
            Assert.Equal(ParentChildType.Biological, loaded.Type);
            Assert.Equal(EndpointApproval.Approved, loaded.ParentApproval);
            Assert.Equal(EndpointApproval.Pending, loaded.ChildApproval);
            Assert.Equal(2, loaded.BirthOrder);
            Assert.False(loaded.IsDeleted);
        }
    }

    [Fact]
    public async Task Changes_to_a_loaded_link_are_saved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var options = await postgres.NewMigratedDatabaseAsync();
        var account = TestData.NewAccount();
        var parent = TestData.NewPerson(account, "Parent");
        var child = TestData.NewPerson(account, "Child");
        var link = TestData.NewLink(parent, child, account);
        link.SetBirthOrder(parent, account, 1);
        var deletedAt = new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

        await using (var context = new FamilyGraphDbContext(options))
        {
            context.AddRange(parent, child, link);
            await context.SaveChangesAsync(cancellationToken);
        }

        await using (var context = new FamilyGraphDbContext(options))
        {
            var loaded = await context.Set<ParentChildLink>().SingleAsync(l => l.Id == link.Id, cancellationToken);
            loaded.SetBirthOrder(parent, account, null);
            loaded.Delete(parent, child, account, deletedAt);
            await context.SaveChangesAsync(cancellationToken);
        }

        await using (var context = new FamilyGraphDbContext(options))
        {
            var loaded = await context.Set<ParentChildLink>().SingleAsync(l => l.Id == link.Id, cancellationToken);

            Assert.Null(loaded.BirthOrder);
            Assert.Equal(deletedAt, loaded.DeletedAt);
            Assert.False(loaded.IsActive);
        }
    }

    [Fact]
    public async Task Database_rejects_a_second_non_deleted_link_between_the_same_parent_and_child()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var options = await postgres.NewMigratedDatabaseAsync();
        var account = TestData.NewAccount();
        var parent = TestData.NewPerson(account, "Parent");
        var child = TestData.NewPerson(account, "Child");

        await using (var context = new FamilyGraphDbContext(options))
        {
            context.AddRange(parent, child, TestData.NewLink(parent, child, account));
            await context.SaveChangesAsync(cancellationToken);
        }

        await using (var context = new FamilyGraphDbContext(options))
        {
            context.AttachRange(parent, child);
            context.Add(TestData.NewLink(parent, child, account));

            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(cancellationToken));
            var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
            Assert.Equal("ux_parent_child_links_parent_child", postgresException.ConstraintName);
        }
    }

    [Fact]
    public async Task A_deleted_link_does_not_block_linking_the_same_parent_and_child_again()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var options = await postgres.NewMigratedDatabaseAsync();
        var account = TestData.NewAccount();
        var parent = TestData.NewPerson(account, "Parent");
        var child = TestData.NewPerson(account, "Child");
        var first = TestData.NewLink(parent, child, account);
        first.Delete(parent, child, account, DateTimeOffset.UtcNow);

        await using var context = new FamilyGraphDbContext(options);
        context.AddRange(parent, child, first, TestData.NewLink(parent, child, account));
        await context.SaveChangesAsync(cancellationToken);

        Assert.Equal(2, await context.Set<ParentChildLink>().CountAsync(cancellationToken));
    }

    [Fact]
    public async Task Database_rejects_a_link_from_a_person_to_themselves()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var options = await postgres.NewMigratedDatabaseAsync();
        var person = TestData.NewPerson(TestData.NewAccount());

        await using var context = new FamilyGraphDbContext(options);
        context.Add(person);
        await context.SaveChangesAsync(cancellationToken);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO parent_child_links (id, parent_id, child_id, type, parent_approval, child_approval)
            VALUES ({Guid.NewGuid()}, {person.Id.Value}, {person.Id.Value}, 1, 2, 2)
            """,
            cancellationToken));
        Assert.Equal("ck_parent_child_links_not_self", exception.ConstraintName);
    }
}
