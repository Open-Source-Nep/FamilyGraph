using FamilyGraph.Application.Accounts;
using FamilyGraph.Application.Export;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.Export;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;
using FamilyGraph.Infrastructure.Persistence;
using FamilyGraph.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyGraph.Infrastructure.Tests.Export;

/// <summary>The export's queries and audit record against PostgreSQL (D-029).</summary>
public class ExportPersistenceTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("AC", "AC-09.4")]
    [Trait("AC", "AC-09.6")]
    [Trait("AC", "AC-09.7")]
    public async Task Export_reads_every_profile_the_non_deleted_links_and_marriages_and_only_maintaining_accounts()
    {
        await using var services = await NewServicesAsync();
        var alice = await CreateAccountAsync(services, "alice@example.org");
        var idle = await CreateAccountAsync(services, "idle@example.org");
        var admin = await CreateAccountAsync(services, "admin@example.org");
        var parent = TestData.NewPerson(alice, "Hari");
        var child = TestData.NewPerson(alice, "Maya");
        var deleted = TestData.NewPerson(alice, "Zebediah");
        child.SetVisibility(ProfileVisibility.Private, alice);
        var kept = TestData.NewLink(parent, child, alice);
        var removed = TestData.NewLink(deleted, child, alice);
        removed.Delete(deleted, child, alice, Now);
        var marriage = Marriage.Create(parent, deleted, alice, alreadyMarried: false, directlyRelated: false);
        marriage.SetFormer(parent, deleted, alice, isFormer: true);
        var deletedMarriage = Marriage.Create(parent, child, alice, alreadyMarried: false, directlyRelated: false);
        deletedMarriage.Delete(parent, child, alice, Now);
        deleted.Delete(alice, actorIsAdministrator: false, Now);
        await SaveAsync(services, parent, child, deleted, kept, removed, marriage, deletedMarriage);

        await using var scope = services.CreateAsyncScope();
        await using var export = await scope.ServiceProvider.GetRequiredService<ExportService>()
            .StartAsync(new Viewer(admin, IsAdministrator: true), Cancellation);
        var accounts = await export.AccountsAsync(Cancellation).ToListAsync(Cancellation);
        var people = await export.PeopleAsync(Cancellation).ToListAsync(Cancellation);
        var links = await export.LinksAsync(Cancellation).ToListAsync(Cancellation);
        var marriages = await export.MarriagesAsync(Cancellation).ToListAsync(Cancellation);

        Assert.Equal([new ExportedAccount(alice, "alice@example.org", true)], accounts);
        Assert.DoesNotContain(accounts, account => account.Id == idle);
        Assert.Equal(
            new HashSet<ExportedPerson>
            {
                new ExportedPerson(parent.Id, new PersonName("Hari"), ProfileVisibility.Public, null, alice, null),
                new ExportedPerson(child.Id, new PersonName("Maya"), ProfileVisibility.Private, null, alice, null),
                new ExportedPerson(deleted.Id, null, ProfileVisibility.Public, Now, alice, null),
            },
            people.ToHashSet());
        Assert.Equal(
            [new ExportedLink(kept.Id, parent.Id, child.Id, ParentChildType.Biological, EndpointApproval.Approved, EndpointApproval.Approved, null)],
            links);
        Assert.Equal(
            [new ExportedMarriage(
                marriage.Id, marriage.FirstSpouseId, marriage.SecondSpouseId, EndpointApproval.Approved, EndpointApproval.Approved, true)],
            marriages);

        await using var check = services.CreateAsyncScope();
        var record = await check.ServiceProvider.GetRequiredService<FamilyGraphDbContext>()
            .Set<AuditEventRecord>().SingleAsync(r => r.Type == nameof(DataExported), Cancellation);
        Assert.Equal((admin.Value, admin.Value), (record.ActorId, record.SubjectId));
    }

    [Fact]
    [Trait("AC", "AC-09.6")]
    public async Task Export_reads_one_moment_even_when_data_changes_while_it_streams()
    {
        await using var services = await NewServicesAsync();
        var alice = await CreateAccountAsync(services, "alice@example.org");
        var before = TestData.NewPerson(alice, "Hari");
        await SaveAsync(services, before);

        await using var scope = services.CreateAsyncScope();
        await using var export = await scope.ServiceProvider.GetRequiredService<ExportService>()
            .StartAsync(new Viewer(alice, IsAdministrator: true), Cancellation);
        _ = await export.AccountsAsync(Cancellation).ToListAsync(Cancellation);
        var later = TestData.NewPerson(alice, "Maya");
        await SaveAsync(services, later);
        var people = await export.PeopleAsync(Cancellation).ToListAsync(Cancellation);

        Assert.Equal([before.Id], people.Select(person => person.Id));
    }

    private static async Task<AccountId> CreateAccountAsync(ServiceProvider services, string email)
    {
        await using var scope = services.CreateAsyncScope();
        var account = await scope.ServiceProvider.GetRequiredService<IAccountStore>()
            .CreateAsync(new ExternalLogin("Google", Guid.NewGuid().ToString(), email, EmailVerified: true), isAdministrator: false, Cancellation);
        return account.Id;
    }

    private static async Task SaveAsync(ServiceProvider services, params object[] entities)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<FamilyGraphDbContext>();
        context.AddRange(entities);
        await context.SaveChangesAsync(Cancellation);
    }

    private async Task<ServiceProvider> NewServicesAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(await postgres.NewMigratedConnectionStringAsync());
        services.AddSingleton<TimeProvider>(new FixedTime(Now));
        services.AddScoped<ExportService>();
        return services.BuildServiceProvider();
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
