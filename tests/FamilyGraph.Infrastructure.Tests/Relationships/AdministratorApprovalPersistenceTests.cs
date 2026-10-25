using FamilyGraph.Application.Graph;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Application.Relationships;
using FamilyGraph.Business.Audit;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;
using FamilyGraph.Infrastructure.Persistence;
using FamilyGraph.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyGraph.Infrastructure.Tests.Relationships;

/// <summary>The administrator's lists of pending links and marriages, and approvals on behalf, against PostgreSQL (D-040).</summary>
public class AdministratorApprovalPersistenceTests(PostgresFixture postgres)
{
    private static readonly AccountId Alice = TestData.NewAccount();
    private static readonly AccountId Bob = TestData.NewAccount();
    private static readonly AccountId Admin = TestData.NewAccount();

    private static readonly Viewer Administrator = new(Admin, IsAdministrator: true);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static AncestorSnapshot NoAncestors(Person person) => new(person.Id, new HashSet<PersonId>(), isComplete: true);

    private static ParentChildLink NewLink(Person parent, Person child, AccountId creator) =>
        ParentChildLink.Create(parent, child, ParentChildType.Biological, creator, new HashSet<PersonId>(), NoAncestors(parent), married: false);

    [Fact]
    [Trait("AC", "AC-03.9")]
    public async Task Pending_links_waiting_for_other_maintainers_are_listed_by_name()
    {
        await using var services = await NewServicesAsync();
        var hari = TestData.NewPerson(Alice, "Hari");
        var maya = TestData.NewPerson(Bob, "Maya");
        var bina = TestData.NewPerson(Bob, "Bina");
        var gita = TestData.NewPerson(Alice, "Gita");
        var ram = TestData.NewPerson(Admin, "Ram");
        var sita = TestData.NewPerson(Alice, "Sita");
        var gone = TestData.NewPerson(Bob, "Gone");
        var waitingForBob = NewLink(hari, maya, Alice);
        var waitingForAlice = NewLink(bina, gita, Bob);
        var waitingForAdmin = NewLink(hari, ram, Alice);
        var declined = NewLink(sita, bina, Alice);
        declined.Disagree(bina, sita, Bob);
        var waitingForDeleted = NewLink(hari, gone, Alice);
        gone.Delete(Bob, actorIsAdministrator: false, DateTimeOffset.UtcNow);
        await SaveAsync(services, hari, maya, bina, gita, ram, sita, gone, waitingForBob, waitingForAlice, waitingForAdmin, declined, waitingForDeleted, NewLink(hari, sita, Alice));

        await using var scope = services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IRelationshipQueries>();
        var list = await queries.ListPendingLinksAsync(Administrator, limit: 10, Cancellation);
        var cut = await queries.ListPendingLinksAsync(Administrator, limit: 1, Cancellation);

        Assert.Equal([waitingForAlice.Id, waitingForBob.Id], list.Links.Select(link => link.Id));
        Assert.False(list.IsTruncated);
        Assert.Equal(EndpointApproval.Pending, list.Links[1].Child.Approval);
        Assert.Equal("Maya", list.Links[1].Child.Person.Name.FirstName);
        Assert.Equal([waitingForAlice.Id], cut.Links.Select(link => link.Id));
        Assert.True(cut.IsTruncated);
    }

    [Fact]
    [Trait("AC", "AC-03.9")]
    public async Task Pending_marriages_waiting_for_other_maintainers_are_listed()
    {
        await using var services = await NewServicesAsync();
        var ram = TestData.NewPerson(Alice, "Ram");
        var sita = TestData.NewPerson(Bob, "Sita");
        var gita = TestData.NewPerson(Admin, "Gita");
        var waiting = Marriage.Create(ram, sita, Alice, alreadyMarried: false, directlyRelated: false);
        var waitingForAdmin = Marriage.Create(ram, gita, Alice, alreadyMarried: false, directlyRelated: false);
        await SaveAsync(services, ram, sita, gita, waiting, waitingForAdmin);

        await using var scope = services.CreateAsyncScope();
        var list = await scope.ServiceProvider.GetRequiredService<IMarriageQueries>()
            .ListPendingMarriagesAsync(Administrator, limit: 10, Cancellation);

        Assert.Equal([waiting.Id], list.Marriages.Select(marriage => marriage.Id));
    }

    [Fact]
    [Trait("AC", "AC-03.6")]
    [Trait("AC", "AC-03.8")]
    public async Task An_administrator_approval_is_saved_with_its_own_audit_event()
    {
        await using var services = await NewServicesAsync();
        var hari = TestData.NewPerson(Alice, "Hari");
        var maya = TestData.NewPerson(Bob, "Maya");
        var ram = TestData.NewPerson(Alice, "Ram");
        var sita = TestData.NewPerson(Bob, "Sita");
        var link = NewLink(hari, maya, Alice);
        var marriage = Marriage.Create(ram, sita, Alice, alreadyMarried: false, directlyRelated: false);
        await SaveAsync(services, hari, maya, ram, sita, link, marriage);

        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<RelationshipService>()
                .ApproveAsAdministratorAsync(link.Id, LinkEndpoint.Child, Administrator, Cancellation);
            await scope.ServiceProvider.GetRequiredService<MarriageService>()
                .ApproveAsAdministratorAsync(marriage.Id, sita.Id, Administrator, Cancellation);
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<FamilyGraphDbContext>();
            Assert.True((await context.Set<ParentChildLink>().SingleAsync(Cancellation)).IsActive);
            Assert.True((await context.Set<Marriage>().SingleAsync(Cancellation)).IsActive);
            var records = await context.Set<AuditEventRecord>()
                .Where(r => r.SubjectId == link.Id.Value || r.SubjectId == marriage.Id.Value)
                .OrderBy(r => r.Id).ToListAsync(Cancellation);
            Assert.Equal(
                [nameof(LinkEndpointApprovedByAdministrator), nameof(MarriageSpouseApprovedByAdministrator)],
                records.Select(r => r.Type));
            Assert.All(records, r => Assert.Equal(AuditFamily.RelationshipConsent.Value, r.Family));
        }
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
        services.AddScoped<RelationshipService>();
        services.AddScoped<MarriageService>();
        return services.BuildServiceProvider();
    }
}
