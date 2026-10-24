using FamilyGraph.Application.Graph;
using FamilyGraph.Application.People;
using FamilyGraph.Application.Persistence;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Application.Relationships;
using FamilyGraph.Business.Audit;
using FamilyGraph.Business.Graph;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;
using FamilyGraph.Infrastructure.Persistence;
using FamilyGraph.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyGraph.Infrastructure.Tests.Relationships;

/// <summary>The marriage store, its queries and the graph walks across marriages against PostgreSQL (D-031).</summary>
public class MarriagePersistenceTests(PostgresFixture postgres)
{
    private static readonly AccountId Maintainer = TestData.NewAccount();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static Viewer As(AccountId account) => new(account, IsAdministrator: false);

    [Fact]
    [Trait("AC", "AC-02.11")]
    [Trait("AC", "AC-02.14")]
    public async Task A_marriage_and_its_former_mark_are_saved_with_their_audit_events()
    {
        await using var services = await NewServicesAsync();
        var ram = TestData.NewPerson(Maintainer, "Ram");
        var sita = TestData.NewPerson(Maintainer, "Sita");
        await SaveAsync(services, ram, sita);

        CreatedMarriage created;
        await using (var scope = services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<MarriageService>();
            created = await service.CreateAsync(sita.Id, ram.Id, Maintainer, Cancellation);
            await service.SetFormerAsync(created.Id, isFormer: true, Maintainer, Cancellation);
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<FamilyGraphDbContext>();
            var marriage = await context.Set<Marriage>().SingleAsync(Cancellation);
            var records = await context.Set<AuditEventRecord>().Where(r => r.SubjectId == created.Id.Value)
                .OrderBy(r => r.Id).ToListAsync(Cancellation);

            Assert.True(marriage.IsActive);
            Assert.True(marriage.IsFormer);
            Assert.Equal(
                [(nameof(MarriageCreated), AuditFamily.RelationshipConsent.Value), (nameof(MarriageFormerChanged), AuditFamily.Profile.Value)],
                records.Select(r => (r.Type, r.Family)));
        }
    }

    [Fact]
    [Trait("AC", "AC-02.13")]
    public async Task The_database_keeps_one_marriage_per_pair_but_allows_one_after_a_deletion()
    {
        await using var services = await NewServicesAsync();
        var ram = TestData.NewPerson(Maintainer, "Ram");
        var sita = TestData.NewPerson(Maintainer, "Sita");
        var deleted = Marriage.Create(ram, sita, Maintainer, alreadyMarried: false, directlyRelated: false);
        deleted.Delete(ram, sita, Maintainer, DateTimeOffset.UtcNow);
        await SaveAsync(services, ram, sita, deleted, Marriage.Create(ram, sita, Maintainer, alreadyMarried: false, directlyRelated: false));

        await using var scope = services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IMarriageStore>();
        Assert.True(await store.ExistsAsync(sita.Id, ram.Id, Cancellation));
        store.Add(Marriage.Create(sita, ram, Maintainer, alreadyMarried: false, directlyRelated: false));

        await Assert.ThrowsAsync<DuplicateMarriageException>(
            () => scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Cancellation));
    }

    [Fact]
    [Trait("AC", "AC-02.12")]
    [Trait("AC", "AC-02.15")]
    public async Task Spouses_list_active_marriages_current_first_and_inactive_ones_wait_for_approval()
    {
        await using var services = await NewServicesAsync();
        var other = TestData.NewAccount();
        var ram = TestData.NewPerson(Maintainer, "Ram");
        var gita = TestData.NewPerson(Maintainer, "Gita");
        var sita = TestData.NewPerson(Maintainer, "Sita");
        var lila = TestData.NewPerson(other, "Lila");
        var former = Marriage.Create(ram, gita, Maintainer, alreadyMarried: false, directlyRelated: false);
        former.SetFormer(ram, gita, Maintainer, isFormer: true);
        var pending = Marriage.Create(lila, ram, Maintainer, alreadyMarried: false, directlyRelated: false);
        await SaveAsync(services, ram, gita, sita, lila, former, Marriage.Create(ram, sita, Maintainer, alreadyMarried: false, directlyRelated: false), pending);

        await using var scope = services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IMarriageQueries>();
        var spouses = await queries.ListSpousesAsync(ram.Id, As(Maintainer), limit: 10, Cancellation);
        var waiting = await queries.ListInactiveMarriagesAsync(As(other), limit: 10, Cancellation);

        Assert.Equal(
            [("Sita", false), ("Gita", true)],
            spouses.Spouses.Select(spouse => (spouse.Person.Name.FirstName, spouse.IsFormer)));
        var marriage = Assert.Single(waiting.Marriages);
        Assert.Equal(pending.Id, marriage.Id);
        Assert.Equal(EndpointApproval.Pending, lila.Id == marriage.First.Person.Id ? marriage.First.Approval : marriage.Second.Approval);
    }

    [Fact]
    [Trait("AC", "AC-08.9")]
    public async Task Graph_walks_cross_active_marriages_in_the_same_generation_but_not_inactive_ones()
    {
        await using var services = await NewServicesAsync();
        var other = TestData.NewAccount();
        var you = TestData.NewPerson(Maintainer, "You");
        var husband = TestData.NewPerson(Maintainer, "Husband");
        var fatherInLaw = TestData.NewPerson(Maintainer, "Father-in-law");
        var stranger = TestData.NewPerson(other, "Stranger");
        await SaveAsync(
            services,
            you, husband, fatherInLaw, stranger,
            Marriage.Create(you, husband, Maintainer, alreadyMarried: false, directlyRelated: false),
            TestData.NewLink(fatherInLaw, husband, Maintainer),
            Marriage.Create(you, stranger, Maintainer, alreadyMarried: false, directlyRelated: false));

        await using var scope = services.CreateAsyncScope();
        var service = new GraphService(
            scope.ServiceProvider.GetRequiredService<IPersonQueries>(),
            scope.ServiceProvider.GetRequiredService<IGraphQueries>(),
            new GraphLimits());
        var view = await service.LocalViewAsync(you.Id, As(Maintainer), Cancellation);
        var overview = await service.OverviewAsync(As(Maintainer), Cancellation);
        var connections = await service.FindConnectionsAsync(you.Id, fatherInLaw.Id, As(Maintainer), Cancellation);

        Assert.Equal(
            [("You", 0), ("Husband", 0), ("Father-in-law", 1)],
            view.People.Select(row => (row.Person.Name!.FirstName, row.Generation)));
        Assert.Single(view.Marriages);
        Assert.Equal(3, overview.People.Count);
        Assert.Single(overview.Marriages);
        Assert.Equal(
            [StepRelation.Spouse, StepRelation.Parent],
            Assert.Single(connections.Paths).Steps.Select(step => step.Relation));
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
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new RelationshipLimits());
        services.AddSingleton(new GraphLimits());
        services.AddScoped<MarriageService>();
        return services.BuildServiceProvider();
    }
}
