using FamilyGraph.Application.Persistence;
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

/// <summary>The link store, the bounded ancestor query, the relatives query and the consent use cases against PostgreSQL (#14, #59).</summary>
public class RelationshipPersistenceServicesTests(PostgresFixture postgres)
{
    private static readonly AccountId Maintainer = TestData.NewAccount();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static Viewer As(AccountId account) => new(account, IsAdministrator: false);

    [Fact]
    [Trait("AC", "AC-02.1")]
    public async Task Created_link_is_saved_with_its_creation_audit_event()
    {
        await using var services = await NewServicesAsync();
        var parent = await SavePersonAsync(services, "Hari");
        var child = await SavePersonAsync(services, "Maya");

        CreatedLink created;
        await using (var scope = services.CreateAsyncScope())
        {
            created = await scope.ServiceProvider.GetRequiredService<RelationshipService>()
                .CreateLinkAsync(parent, child, ParentChildType.Adoptive, Maintainer, Cancellation);
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<FamilyGraphDbContext>();
            var link = await context.Set<ParentChildLink>().SingleAsync(Cancellation);
            var record = await context.Set<AuditEventRecord>().SingleAsync(r => r.Type == nameof(LinkCreated), Cancellation);

            Assert.Equal(created.Id, link.Id);
            Assert.Equal(ParentChildType.Adoptive, link.Type);
            Assert.True(link.IsActive);
            Assert.Equal(AuditFamily.RelationshipConsent.Value, record.Family);
            Assert.Equal(link.Id.Value, record.SubjectId);
        }
    }

    [Fact]
    public async Task Parents_of_counts_pending_links_but_not_deleted_ones()
    {
        await using var services = await NewServicesAsync();
        var child = TestData.NewPerson(Maintainer, "Maya");
        var pendingParent = TestData.NewPerson(TestData.NewAccount(), "Gita");
        var deletedParent = TestData.NewPerson(Maintainer, "Hari");
        var pending = TestData.NewLink(pendingParent, child, Maintainer);
        var deleted = TestData.NewLink(deletedParent, child, Maintainer);
        deleted.Delete(deletedParent, child, Maintainer, DateTimeOffset.UtcNow);
        await SaveAsync(services, child, pendingParent, deletedParent, pending, deleted);

        await using var scope = services.CreateAsyncScope();
        var parents = await scope.ServiceProvider.GetRequiredService<IParentChildLinkStore>().ParentsOfAsync(child.Id, Cancellation);

        Assert.Equal([pendingParent.Id], parents);
    }

    [Fact]
    public async Task Duplicate_that_slips_past_the_domain_check_is_reported_as_a_duplicate()
    {
        await using var services = await NewServicesAsync();
        var parent = TestData.NewPerson(Maintainer, "Hari");
        var child = TestData.NewPerson(Maintainer, "Maya");
        await SaveAsync(services, parent, child, TestData.NewLink(parent, child, Maintainer));

        await using var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IParentChildLinkStore>().Add(TestData.NewLink(parent, child, Maintainer));

        await Assert.ThrowsAsync<DuplicateLinkException>(
            () => scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Cancellation));
    }

    [Fact]
    [Trait("AC", "AC-02.6")]
    public async Task Ancestors_include_every_generation_through_non_deleted_links_once()
    {
        await using var services = await NewServicesAsync();
        // Two parents who share both grandparents: the grandparents are reached by two paths.
        var person = TestData.NewPerson(Maintainer, "Maya");
        var mother = TestData.NewPerson(TestData.NewAccount(), "Sita");
        var father = TestData.NewPerson(Maintainer, "Hari");
        var grandmother = TestData.NewPerson(Maintainer, "Gita");
        var grandfather = TestData.NewPerson(Maintainer, "Ram");
        var removed = TestData.NewPerson(Maintainer, "Removed");
        var deleted = TestData.NewLink(removed, person, Maintainer);
        deleted.Delete(removed, person, Maintainer, DateTimeOffset.UtcNow);
        await SaveAsync(
            services,
            person, mother, father, grandmother, grandfather, removed, deleted,
            TestData.NewLink(mother, person, Maintainer),
            TestData.NewLink(father, person, Maintainer),
            TestData.NewLink(grandmother, mother, Maintainer),
            TestData.NewLink(grandmother, father, Maintainer),
            TestData.NewLink(grandfather, mother, Maintainer),
            TestData.NewLink(grandfather, father, Maintainer));

        await using var scope = services.CreateAsyncScope();
        var snapshot = await scope.ServiceProvider.GetRequiredService<IParentChildLinkStore>().AncestorsOfAsync(person.Id, Cancellation);

        Assert.Equal(person.Id, snapshot.PersonId);
        Assert.True(snapshot.IsComplete);
        Assert.Equal(
            new HashSet<PersonId> { mother.Id, father.Id, grandmother.Id, grandfather.Id },
            snapshot.Ancestors.ToHashSet());
    }

    [Fact]
    [Trait("AC", "AC-02.7")]
    public async Task Ancestor_chain_longer_than_the_generation_limit_is_incomplete()
    {
        await using var services = await NewServicesAsync();
        var people = Enumerable.Range(0, AncestorSnapshot.MaxGenerations + 2)
            .Select(index => TestData.NewPerson(Maintainer, $"P{index}"))
            .ToList();
        // people[i + 1] is the parent of people[i], so people[0] has MaxGenerations + 1 ancestors.
        var links = people.Zip(people.Skip(1), (child, parent) => TestData.NewLink(parent, child, Maintainer));
        await SaveAsync(services, [.. people, .. links]);

        await using var scope = services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IParentChildLinkStore>();
        var cut = await store.AncestorsOfAsync(people[0].Id, Cancellation);
        var whole = await store.AncestorsOfAsync(people[1].Id, Cancellation);

        Assert.False(cut.IsComplete);
        Assert.Equal(AncestorSnapshot.MaxGenerations, cut.Ancestors.Count);
        Assert.DoesNotContain(people[^1].Id, cut.Ancestors);
        Assert.True(whole.IsComplete);
        Assert.Equal(AncestorSnapshot.MaxGenerations, whole.Ancestors.Count);
    }

    [Fact]
    public async Task Ancestor_query_stops_on_a_cycle_already_in_the_data()
    {
        await using var services = await NewServicesAsync();
        var first = TestData.NewPerson(Maintainer, "First");
        var second = TestData.NewPerson(Maintainer, "Second");
        // TestData skips the cycle check, so this stores a cycle the domain would reject.
        await SaveAsync(services, first, second, TestData.NewLink(first, second, Maintainer), TestData.NewLink(second, first, Maintainer));

        await using var scope = services.CreateAsyncScope();
        var snapshot = await scope.ServiceProvider.GetRequiredService<IParentChildLinkStore>().AncestorsOfAsync(first.Id, Cancellation);

        Assert.Equal(new HashSet<PersonId> { first.Id, second.Id }, snapshot.Ancestors.ToHashSet());
    }

    [Fact]
    [Trait("AC", "AC-02.5")]
    public async Task Relatives_are_active_links_only_with_children_in_birth_order_then_by_name()
    {
        await using var services = await NewServicesAsync();
        var parent = TestData.NewPerson(Maintainer, "Hari");
        var grandparent = TestData.NewPerson(Maintainer, "Ram");
        var pendingParent = TestData.NewPerson(TestData.NewAccount(), "Gita");
        var asha = TestData.NewPerson(Maintainer, "Asha");
        var bina = TestData.NewPerson(Maintainer, "Bina");
        var chandra = TestData.NewPerson(Maintainer, "Chandra");
        var deleted = TestData.NewPerson(Maintainer, "Deleted");
        var chandraLink = TestData.NewLink(parent, chandra, Maintainer);
        chandraLink.SetBirthOrder(parent, Maintainer, 1);
        var deletedLink = TestData.NewLink(parent, deleted, Maintainer);
        deletedLink.Delete(parent, deleted, Maintainer, DateTimeOffset.UtcNow);
        await SaveAsync(
            services,
            parent, grandparent, pendingParent, asha, bina, chandra, deleted, chandraLink, deletedLink,
            TestData.NewLink(grandparent, parent, Maintainer),
            TestData.NewLink(pendingParent, parent, Maintainer),
            TestData.NewLink(parent, bina, Maintainer),
            TestData.NewLink(parent, asha, Maintainer));

        await using var scope = services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IRelationshipQueries>();
        var all = await queries.ListDirectRelativesAsync(parent.Id, As(Maintainer), limit: 10, Cancellation);
        var cut = await queries.ListDirectRelativesAsync(parent.Id, As(Maintainer), limit: 2, Cancellation);

        Assert.Equal(["Ram"], all.Parents.Select(relative => relative.Person.Name.FirstName));
        Assert.True(all.Parents[0].Person.MaintainedByViewer);
        Assert.Equal(ParentChildType.Biological, all.Parents[0].Type);
        Assert.Equal(["Chandra", "Asha", "Bina"], all.Children.Select(relative => relative.Person.Name.FirstName));
        Assert.Equal([1, null, null], all.Children.Select(relative => relative.BirthOrder));
        Assert.False(all.ChildrenTruncated);
        Assert.Equal(["Chandra", "Asha"], cut.Children.Select(relative => relative.Person.Name.FirstName));
        Assert.True(cut.ChildrenTruncated);
        Assert.False(cut.ParentsTruncated);
    }

    [Fact]
    [Trait("AC", "AC-02.17")]
    public async Task Unordered_children_sort_by_birth_dates_shown_to_others_then_by_name()
    {
        await using var services = await NewServicesAsync();
        var parent = TestData.NewPerson(Maintainer, "Hari");
        var asha = TestData.NewPerson(Maintainer, "Asha");
        var bina = Person.Create(new PersonName("Bina"), Maintainer, birthDate: BirthDate.Ad(1990), birthDateShown: true);
        var chandra = Person.Create(new PersonName("Chandra"), Maintainer, birthDate: BirthDate.Ad(1985, 3), birthDateShown: true);
        // A hidden birth date never orders children, not even for its maintainer.
        var dipa = Person.Create(new PersonName("Dipa"), Maintainer, birthDate: BirthDate.Ad(1970), birthDateShown: false);
        var ganga = TestData.NewPerson(Maintainer, "Ganga");
        var gangaLink = TestData.NewLink(parent, ganga, Maintainer);
        gangaLink.SetBirthOrder(parent, Maintainer, 1);
        await SaveAsync(
            services,
            parent, asha, bina, chandra, dipa, ganga, gangaLink,
            TestData.NewLink(parent, asha, Maintainer),
            TestData.NewLink(parent, bina, Maintainer),
            TestData.NewLink(parent, chandra, Maintainer),
            TestData.NewLink(parent, dipa, Maintainer));

        await using var scope = services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IRelationshipQueries>();
        var relatives = await queries.ListDirectRelativesAsync(parent.Id, As(Maintainer), limit: 10, Cancellation);

        Assert.Equal(
            ["Ganga", "Chandra", "Bina", "Asha", "Dipa"],
            relatives.Children.Select(relative => relative.Person.Name.FirstName));
    }

    [Fact]
    [Trait("AC", "AC-03.1")]
    [Trait("AC", "AC-03.3")]
    public async Task Approval_and_withdrawal_are_saved_with_their_audit_events()
    {
        await using var services = await NewServicesAsync();
        var other = TestData.NewAccount();
        var parent = TestData.NewPerson(other, "Hari");
        var child = TestData.NewPerson(Maintainer, "Maya");
        var link = TestData.NewLink(parent, child, Maintainer);
        await SaveAsync(services, parent, child, link);

        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<RelationshipService>()
                .ApproveAsync(link.Id, LinkEndpoint.Parent, As(other), Cancellation);
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<FamilyGraphDbContext>();
            Assert.True((await context.Set<ParentChildLink>().SingleAsync(Cancellation)).IsActive);
            await scope.ServiceProvider.GetRequiredService<RelationshipService>()
                .DisagreeAsync(link.Id, LinkEndpoint.Child, As(Maintainer), Cancellation);
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<FamilyGraphDbContext>();
            var saved = await context.Set<ParentChildLink>().SingleAsync(Cancellation);
            var types = await context.Set<AuditEventRecord>()
                .Where(record => record.SubjectId == link.Id.Value)
                .Select(record => record.Type)
                .OrderBy(type => type)
                .ToListAsync(Cancellation);

            Assert.Equal((EndpointApproval.Approved, EndpointApproval.Disagreed), (saved.ParentApproval, saved.ChildApproval));
            Assert.False(saved.IsActive);
            Assert.Equal([nameof(LinkEndpointApproved), nameof(LinkEndpointDisagreed)], types);
        }
    }

    [Fact]
    public async Task Inactive_links_are_those_of_the_viewers_profiles_waiting_for_the_viewer_first()
    {
        await using var services = await NewServicesAsync();
        var other = TestData.NewAccount();
        var maya = TestData.NewPerson(Maintainer, "Maya");
        var hari = TestData.NewPerson(Maintainer, "Hari");
        var bina = TestData.NewPerson(other, "Bina");
        var ram = TestData.NewPerson(other, "Ram");
        var gita = TestData.NewPerson(other, "Gita");
        var waitingForOther = TestData.NewLink(bina, maya, Maintainer);
        var waitingForViewer = TestData.NewLink(ram, hari, other);
        var deleted = TestData.NewLink(hari, maya, Maintainer);
        deleted.Delete(hari, maya, Maintainer, DateTimeOffset.UtcNow);
        await SaveAsync(
            services,
            maya, hari, bina, ram, gita, waitingForOther, waitingForViewer, deleted,
            TestData.NewLink(ram, maya, Maintainer),
            TestData.NewLink(ram, gita, other));
        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<RelationshipService>()
                .ApproveAsync(waitingForOther.Id, LinkEndpoint.Parent, As(other), Cancellation);
            await scope.ServiceProvider.GetRequiredService<RelationshipService>()
                .DisagreeAsync(waitingForOther.Id, LinkEndpoint.Child, As(Maintainer), Cancellation);
        }

        await using var queryScope = services.CreateAsyncScope();
        var queries = queryScope.ServiceProvider.GetRequiredService<IRelationshipQueries>();
        var all = await queries.ListInactiveLinksAsync(As(Maintainer), limit: 10, Cancellation);
        var cut = await queries.ListInactiveLinksAsync(As(Maintainer), limit: 1, Cancellation);

        Assert.Equal(
            ["Ram → Hari", "Bina → Maya", "Ram → Maya"],
            all.Links.Select(link => $"{link.Parent.Person.Name.FirstName} → {link.Child.Person.Name.FirstName}"));
        Assert.False(all.IsTruncated);
        var first = all.Links[0];
        Assert.Equal(waitingForViewer.Id, first.Id);
        Assert.Equal((EndpointApproval.Approved, false), (first.Parent.Approval, first.Parent.Person.MaintainedByViewer));
        Assert.Equal((EndpointApproval.Pending, true), (first.Child.Approval, first.Child.Person.MaintainedByViewer));
        Assert.Equal(EndpointApproval.Disagreed, all.Links[1].Child.Approval);
        Assert.All(all.Links, link => Assert.False(link.IsActive));
        Assert.Equal(waitingForViewer.Id, Assert.Single(cut.Links).Id);
        Assert.True(cut.IsTruncated);
    }

    [Fact]
    [Trait("AC", "AC-03.4")]
    public async Task Deletion_is_saved_with_its_audit_event_and_the_pair_can_be_linked_again()
    {
        await using var services = await NewServicesAsync();
        var parent = TestData.NewPerson(Maintainer, "Hari");
        var child = TestData.NewPerson(Maintainer, "Maya");
        var link = TestData.NewLink(parent, child, Maintainer);
        await SaveAsync(services, parent, child, link);

        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<RelationshipService>()
                .DeleteAsync(link.Id, Maintainer, Cancellation);
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<FamilyGraphDbContext>();
            var saved = await context.Set<ParentChildLink>().SingleAsync(Cancellation);
            var types = await context.Set<AuditEventRecord>()
                .Where(record => record.SubjectId == link.Id.Value)
                .Select(record => record.Type)
                .ToListAsync(Cancellation);

            Assert.True(saved.IsDeleted);
            Assert.Equal([nameof(LinkDeleted)], types);
            var again = await scope.ServiceProvider.GetRequiredService<RelationshipService>()
                .CreateLinkAsync(parent.Id, child.Id, ParentChildType.Biological, Maintainer, Cancellation);
            Assert.NotEqual(link.Id, again.Id);
        }
    }

    private static async Task<PersonId> SavePersonAsync(ServiceProvider services, string firstName)
    {
        var person = TestData.NewPerson(Maintainer, firstName);
        await SaveAsync(services, person);
        return person.Id;
    }

    [Fact]
    [Trait("AC", "AC-05.1")]
    public async Task Relatives_hidden_from_the_viewer_come_last_by_id_and_carry_their_visibility()
    {
        await using var services = await NewServicesAsync();
        var parent = TestData.NewPerson(Maintainer, "Ram");
        var hiddenParent = TestData.NewPerson(Maintainer, "Anil");
        var shownParent = TestData.NewPerson(Maintainer, "Zed");
        var hiddenChild = TestData.NewPerson(Maintainer, "Asha");
        var shownChild = TestData.NewPerson(Maintainer, "Bina");
        hiddenParent.SetVisibility(ProfileVisibility.Private, Maintainer);
        hiddenChild.SetVisibility(ProfileVisibility.Private, Maintainer);
        await SaveAsync(
            services,
            parent, hiddenParent, shownParent, hiddenChild, shownChild,
            TestData.NewLink(hiddenParent, parent, Maintainer),
            TestData.NewLink(shownParent, parent, Maintainer),
            TestData.NewLink(parent, hiddenChild, Maintainer),
            TestData.NewLink(parent, shownChild, Maintainer));

        await using var scope = services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IRelationshipQueries>();
        var asOther = await queries.ListDirectRelativesAsync(parent.Id, As(TestData.NewAccount()), limit: 10, Cancellation);
        var asAdministrator = await queries.ListDirectRelativesAsync(
            parent.Id, new Viewer(TestData.NewAccount(), IsAdministrator: true), limit: 10, Cancellation);

        Assert.Equal([shownParent.Id, hiddenParent.Id], asOther.Parents.Select(relative => relative.Person.Id));
        Assert.Equal([shownChild.Id, hiddenChild.Id], asOther.Children.Select(relative => relative.Person.Id));
        Assert.Equal(
            [ProfileVisibility.Public, ProfileVisibility.Private],
            asOther.Parents.Select(relative => relative.Person.Visibility));
        Assert.Equal(["Anil", "Zed"], asAdministrator.Parents.Select(relative => relative.Person.Name.FirstName));
        Assert.Equal(["Asha", "Bina"], asAdministrator.Children.Select(relative => relative.Person.Name.FirstName));
    }

    [Fact]
    [Trait("AC", "AC-05.1")]
    public async Task Inactive_links_put_endpoints_hidden_from_the_viewer_last()
    {
        await using var services = await NewServicesAsync();
        var viewer = TestData.NewAccount();
        var child = TestData.NewPerson(viewer, "Maya");
        var hiddenParent = TestData.NewPerson(Maintainer, "Anil");
        var shownParent = TestData.NewPerson(Maintainer, "Zed");
        hiddenParent.SetVisibility(ProfileVisibility.Private, Maintainer);
        var hiddenLink = TestData.NewLink(hiddenParent, child, viewer);
        var shownLink = TestData.NewLink(shownParent, child, viewer);
        await SaveAsync(services, child, hiddenParent, shownParent, hiddenLink, shownLink);

        await using var scope = services.CreateAsyncScope();
        var list = await scope.ServiceProvider.GetRequiredService<IRelationshipQueries>()
            .ListInactiveLinksAsync(As(viewer), limit: 10, Cancellation);

        Assert.Equal([shownLink.Id, hiddenLink.Id], list.Links.Select(link => link.Id));
        Assert.Equal(ProfileVisibility.Private, list.Links[1].Parent.Person.Visibility);
    }

    [Fact]
    public async Task Inactive_links_leave_out_those_whose_only_endpoint_you_maintain_was_deleted()
    {
        await using var services = await NewServicesAsync();
        var former = TestData.NewAccount();
        var deleted = TestData.NewPerson(former, "Hari");
        var child = TestData.NewPerson(Maintainer, "Maya");
        var link = TestData.NewLink(deleted, child, Maintainer);
        deleted.Delete(former, actorIsAdministrator: false, DateTimeOffset.UtcNow);
        await SaveAsync(services, deleted, child, link);

        await using var scope = services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IRelationshipQueries>();
        var asFormer = await queries.ListInactiveLinksAsync(As(former), limit: 10, Cancellation);
        var asOther = await queries.ListInactiveLinksAsync(As(Maintainer), limit: 10, Cancellation);

        Assert.Empty(asFormer.Links);
        Assert.True(Assert.Single(asOther.Links).Parent.Person.IsDeleted);
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
        services.AddScoped<RelationshipService>();
        return services.BuildServiceProvider();
    }
}
