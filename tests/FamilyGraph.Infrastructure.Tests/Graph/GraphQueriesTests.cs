using System.Diagnostics;
using FamilyGraph.Application.Graph;
using FamilyGraph.Application.People;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Application.Relationships;
using FamilyGraph.Business.Graph;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;
using FamilyGraph.Infrastructure.Persistence;
using FamilyGraph.Infrastructure.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyGraph.Infrastructure.Tests.Graph;

/// <summary>The bounded local view, overview, connection search and Bansawali against PostgreSQL (#20, D-009, D-023, D-043).</summary>
public class GraphQueriesTests(PostgresFixture postgres)
{
    private static readonly AccountId Maintainer = TestData.NewAccount();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static Viewer As(AccountId account) => new(account, IsAdministrator: false);

    [Fact]
    [Trait("AC", "AC-08.4")]
    public async Task Local_view_lists_the_nearest_people_first_within_the_generation_window()
    {
        await using var services = await NewServicesAsync();
        var family = Family.Create();
        await SaveAsync(services, family.Entities);

        var view = await LocalViewAsync(services, family.You.Id, generations: 2, peopleLimit: 500);

        Assert.Equal(
            [
                ("You", 0),
                ("Mother", 1), ("Daughter", -1),
                ("Sister", 0), ("Grandmother", 2), ("Granddaughter", -2),
                ("Aunt", 1),
                ("Cousin", 0),
            ],
            view.People.Select(row => (row.Person.Name!.FirstName, row.Generation)));
        Assert.Contains(view.People, row => row.Person.Name!.FirstName == "Grandmother" && row.HasMore);
        Assert.DoesNotContain(view.People, row => row.Person.Name!.FirstName == "Great-grandmother");
        Assert.Equal(7, view.Links.Count);
        Assert.False(view.IsTruncated);
    }

    [Fact]
    [Trait("AC", "AC-02.17")]
    public async Task Local_view_links_carry_the_birth_order_and_people_their_birth_date()
    {
        await using var services = await NewServicesAsync();
        var you = Person.Create(new PersonName("You"), Maintainer, birthDate: BirthDate.Ad(1960, 5, 1), birthDateShown: true);
        var daughter = TestData.NewPerson(Maintainer, "Daughter");
        var son = TestData.NewPerson(Maintainer, "Son");
        var daughterLink = TestData.NewLink(you, daughter, Maintainer);
        daughterLink.SetBirthOrder(you, Maintainer, 2);
        await SaveAsync(services, [you, daughter, son, daughterLink, TestData.NewLink(you, son, Maintainer)]);

        var view = await LocalViewAsync(services, you.Id, generations: 2, peopleLimit: 10);

        Assert.Equal(2, view.Links.Single(link => link.ChildId == daughter.Id).BirthOrder);
        Assert.Null(view.Links.Single(link => link.ChildId == son.Id).BirthOrder);
        var centre = view.People.Single(row => row.Person.Id == you.Id).Person;
        Assert.True(centre.BirthDateShown);
        Assert.Equal(new DateOnly(1960, 5, 1), centre.BirthDate!.SortDate);
    }

    [Fact]
    [Trait("AC", "AC-08.5")]
    public async Task Local_view_stops_at_its_people_limit_and_flags_the_edge()
    {
        await using var services = await NewServicesAsync();
        var family = Family.Create();
        await SaveAsync(services, family.Entities);

        var view = await LocalViewAsync(services, family.You.Id, generations: 2, peopleLimit: 2);

        Assert.Equal(["You", "Mother", "Daughter"], view.People.Select(row => row.Person.Name!.FirstName));
        Assert.True(view.IsTruncated);
        Assert.Equal([false, true, true], view.People.Select(row => row.HasMore));
        Assert.Equal(2, view.Links.Count);
    }

    [Fact]
    [Trait("AC", "AC-08.1")]
    [Trait("AC", "AC-05.5")]
    public async Task Inactive_links_are_never_walked_and_a_restored_link_is_walked_at_once()
    {
        await using var services = await NewServicesAsync();
        var other = TestData.NewAccount();
        var you = TestData.NewPerson(Maintainer, "You");
        var mother = TestData.NewPerson(other, "Mother");
        var link = TestData.NewLink(mother, you, Maintainer);
        await SaveAsync(services, [you, mother, link]);

        var pending = await LocalViewAsync(services, you.Id, generations: 2, peopleLimit: 10);
        await ApproveAsync(services, link.Id, other);
        var active = await LocalViewAsync(services, you.Id, generations: 2, peopleLimit: 10);
        await DisagreeAsync(services, link.Id, other);
        var withdrawn = await LocalViewAsync(services, you.Id, generations: 2, peopleLimit: 10);

        Assert.Equal(["You"], pending.People.Select(row => row.Person.Name!.FirstName));
        Assert.Empty(pending.Links);
        Assert.False(Assert.Single(pending.People).HasMore);
        Assert.Equal(["You", "Mother"], active.People.Select(row => row.Person.Name!.FirstName));
        Assert.Equal(link.Id, Assert.Single(active.Links).Id);
        Assert.Equal(["You"], withdrawn.People.Select(row => row.Person.Name!.FirstName));
    }

    [Fact]
    [Trait("AC", "AC-08.6")]
    public async Task Connections_find_the_shortest_path_and_further_ones_through_active_links()
    {
        await using var services = await NewServicesAsync();
        var family = Family.Create();
        await SaveAsync(services, family.Entities);

        var connections = await ConnectionsAsync(services, family.You.Id, family.Cousin.Id);

        Assert.True(connections.SearchComplete);
        Assert.False(connections.MorePathsExist);
        var path = Assert.Single(connections.Paths);
        Assert.Equal("You", path.Start.Name!.FirstName);
        Assert.Equal(
            [("Parent", "Mother"), ("Parent", "Grandmother"), ("Child", "Aunt"), ("Child", "Cousin")],
            path.Steps.Select(step => (step.Relation.Name, step.Person.Name!.FirstName)));
    }

    [Fact]
    public async Task Connection_search_beyond_its_limit_says_it_was_cut_short()
    {
        await using var services = await NewServicesAsync();
        var family = Family.Create();
        await SaveAsync(services, family.Entities);

        var connections = await ConnectionsAsync(services, family.You.Id, family.Cousin.Id, pathSearchPeople: 3);

        Assert.Empty(connections.Paths);
        Assert.False(connections.SearchComplete);
    }

    [Fact]
    [Trait("AC", "AC-08.4")]
    public async Task Overview_walks_active_links_from_every_profile_you_maintain_nearest_first()
    {
        await using var services = await NewServicesAsync();
        var other = TestData.NewAccount();
        var you = TestData.NewPerson(Maintainer, "You");
        var second = TestData.NewPerson(Maintainer, "Second");
        var mother = TestData.NewPerson(other, "Mother");
        var grandmother = TestData.NewPerson(other, "Grandmother");
        var stranger = TestData.NewPerson(other, "Stranger");
        var motherLink = TestData.NewLink(mother, you, Maintainer);
        var pending = TestData.NewLink(stranger, second, Maintainer);
        await SaveAsync(services, [you, second, mother, grandmother, stranger, motherLink, pending,
            TestData.NewLink(grandmother, mother, other)]);
        await ApproveAsync(services, motherLink.Id, other);

        var overview = await OverviewAsync(services, overviewPeople: 500);
        var cut = await OverviewAsync(services, overviewPeople: 3);

        Assert.Equal(["Second", "You", "Mother", "Grandmother"], overview.People.Select(person => person.Name!.FirstName));
        Assert.Equal(2, overview.Links.Count);
        Assert.False(overview.IsTruncated);
        Assert.Equal(["Second", "You", "Mother"], cut.People.Select(person => person.Name!.FirstName));
        Assert.Equal(motherLink.Id, Assert.Single(cut.Links).Id);
        Assert.True(cut.IsTruncated);
    }

    [Fact]
    [Trait("AC", "AC-08.4")]
    public async Task Graph_queries_on_more_than_ten_thousand_people_stay_within_their_limits()
    {
        await using var services = await NewServicesAsync();
        var (first, middle, last) = await SeedLargeGraphAsync(services, generations: 12, couplesPerGeneration: 450, childrenPerCouple: 2);

        var watch = Stopwatch.StartNew();
        var view = await LocalViewAsync(services, middle, generations: GraphLimits.MaxViewGenerations, peopleLimit: GraphLimits.MaxViewPeople);
        var viewTime = watch.Elapsed;
        watch.Restart();
        var connections = await ConnectionsAsync(services, last, first);
        var searchTime = watch.Elapsed;

        Assert.InRange(view.People.Count, 100, GraphLimits.MaxViewPeople + 1);
        Assert.True(viewTime < TimeSpan.FromSeconds(5), $"Local view took {viewTime}.");
        Assert.NotEmpty(connections.Paths);
        Assert.False(connections.SearchComplete);
        Assert.True(searchTime < TimeSpan.FromSeconds(10), $"Connection search took {searchTime}.");
    }

    /// <summary>
    /// Generations of couples; each couple has two children, who pair up with children of the next
    /// couple. Returns a person in the first, the middle and the last generation.
    /// </summary>
    private static async Task<(PersonId First, PersonId Middle, PersonId Last)> SeedLargeGraphAsync(
        ServiceProvider services, int generations, int couplesPerGeneration, int childrenPerCouple)
    {
        var people = new List<Person>();
        var links = new List<ParentChildLink>();
        var parents = Enumerable.Range(0, 2 * couplesPerGeneration).Select(i => TestData.NewPerson(Maintainer, $"P0-{i}")).ToList();
        people.AddRange(parents);
        for (var generation = 1; generation < generations; generation++)
        {
            var children = new List<Person>();
            for (var couple = 0; couple < couplesPerGeneration; couple++)
            {
                for (var c = 0; c < childrenPerCouple; c++)
                {
                    var child = TestData.NewPerson(Maintainer, $"P{generation}-{children.Count}");
                    children.Add(child);
                    links.Add(TestData.NewLink(parents[2 * couple], child, Maintainer));
                    links.Add(TestData.NewLink(parents[(2 * couple) + 1], child, Maintainer));
                }
            }

            // Pair each child with a child of the neighbouring couple, so the families interconnect.
            parents = [.. children.Skip(1).Append(children[0])];
            people.AddRange(children);
        }

        Assert.True(people.Count > 10_000);
        await SaveAsync(services, [.. people, .. links]);

        // A live database has planner statistics from autovacuum; a freshly bulk-loaded one does not yet.
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<FamilyGraphDbContext>().Database
            .ExecuteSqlRawAsync("ANALYZE people, parent_child_links", Cancellation);
        return (people[0].Id, people[people.Count / 2].Id, people[^1].Id);
    }

    [Fact]
    [Trait("AC", "AC-05.2")]
    [Trait("AC", "AC-05.3")]
    [Trait("AC", "AC-05.4")]
    [Trait("AC", "AC-05.14")]
    public async Task Graph_reads_load_visibility_and_deletion_so_placeholders_stay_on_the_walk()
    {
        await using var services = await NewServicesAsync();
        var grandmother = TestData.NewPerson(Maintainer, "Gita");
        var mother = TestData.NewPerson(Maintainer, "Sita");
        var you = TestData.NewPerson(Maintainer, "Maya");
        mother.SetVisibility(ProfileVisibility.Private, Maintainer);
        mother.SetGender(Gender.Female, Maintainer);
        you.SetGender(Gender.Female, Maintainer);
        var links = new[] { TestData.NewLink(grandmother, mother, Maintainer), TestData.NewLink(mother, you, Maintainer) };
        grandmother.Delete(Maintainer, actorIsAdministrator: false, DateTimeOffset.UtcNow);
        await SaveAsync(services, [grandmother, mother, you, .. links]);

        await using var scope = services.CreateAsyncScope();
        var service = new GraphService(
            scope.ServiceProvider.GetRequiredService<IPersonQueries>(),
            scope.ServiceProvider.GetRequiredService<IGraphQueries>(),
            new GraphLimits());
        var viewer = As(TestData.NewAccount());
        var view = await service.LocalViewAsync(you.Id, viewer, Cancellation);
        var connections = await service.FindConnectionsAsync(you.Id, grandmother.Id, viewer, Cancellation);

        Assert.Equal(
            [("Maya", null, Gender.Female), (null, PersonPlaceholder.Private, null), (null, PersonPlaceholder.Deleted, null)],
            view.People.Select(row => (row.Person.Name?.FirstName, row.Person.Placeholder, row.Person.Gender)));
        Assert.Equal(
            [PersonPlaceholder.Private, PersonPlaceholder.Deleted],
            Assert.Single(connections.Paths).Steps.Select(step => step.Person.Placeholder));
    }

    [Fact]
    [Trait("AC", "AC-08.21")]
    [Trait("AC", "AC-08.23")]
    public async Task Bansawali_follows_sons_over_active_links_with_the_marriages_of_its_people()
    {
        await using var services = await NewServicesAsync();
        var hari = Gendered("Hari", Gender.Male);
        var ram = Gendered("Ram", Gender.Male);
        var sita = Gendered("Sita", Gender.Female);
        var shyam = Gendered("Shyam", Gender.Male);
        var gita = Gendered("Gita", Gender.Female);
        var gauri = Gendered("Gauri", Gender.Female);
        var stranger = TestData.NewPerson(TestData.NewAccount(), "Pending");
        stranger.SetGender(Gender.Male, stranger.MaintainerId);
        var marriage = Marriage.Create(hari, gauri, Maintainer, alreadyMarried: false, directlyRelated: false);
        await SaveAsync(services, [
            hari, ram, sita, shyam, gita, gauri, stranger, marriage,
            TestData.NewLink(hari, ram, Maintainer),
            TestData.NewLink(hari, sita, Maintainer),
            TestData.NewLink(ram, shyam, Maintainer),
            TestData.NewLink(sita, gita, Maintainer),
            TestData.NewLink(hari, stranger, Maintainer),
        ]);

        var bansawali = await BansawaliAsync(services, hari.Id, BansawaliLine.Sons);
        var all = await BansawaliAsync(services, hari.Id, BansawaliLine.AllChildren);

        Assert.Equal(
            [("Hari", 0), ("Ram", 1), ("Sita", 1), ("Shyam", 2)],
            bansawali.People.Select(row => (row.Person.Name!.FirstName, row.Generation)).OrderBy(row => row.Item2).ThenBy(row => row.Item1));
        Assert.Equal(3, bansawali.Links.Count);
        Assert.Equal("Gauri", Assert.Single(bansawali.Spouses).Name!.FirstName);
        Assert.Equal(marriage.Id, Assert.Single(bansawali.Marriages).Id);
        Assert.Equal(5, all.People.Count);
        Assert.False(all.IsTruncated);
    }

    [Fact]
    [Trait("AC", "AC-08.21")]
    public async Task Bansawali_stops_before_a_generation_past_the_limit()
    {
        await using var services = await NewServicesAsync();
        var hari = Gendered("Hari", Gender.Male);
        var ram = Gendered("Ram", Gender.Male);
        var children = Enumerable.Range(1, 3).Select(index => Gendered($"Child {index}", Gender.Male)).ToList();
        await SaveAsync(services, [hari, ram, .. children, TestData.NewLink(hari, ram, Maintainer),
            .. children.Select(child => TestData.NewLink(ram, child, Maintainer))]);

        var bansawali = await BansawaliAsync(services, hari.Id, BansawaliLine.Sons, bansawaliPeople: 3);

        Assert.Equal([("Hari", false), ("Ram", true)], bansawali.People.Select(row => (row.Person.Name!.FirstName, row.HasMore)));
        Assert.True(bansawali.IsTruncated);
    }

    private static Person Gendered(string firstName, Gender gender)
    {
        var person = TestData.NewPerson(Maintainer, firstName);
        person.SetGender(gender, Maintainer);
        return person;
    }

    private static async Task<Bansawali> BansawaliAsync(
        ServiceProvider services, PersonId root, BansawaliLine line, int bansawaliPeople = GraphLimits.DefaultBansawaliPeople)
    {
        await using var scope = services.CreateAsyncScope();
        var service = new GraphService(
            scope.ServiceProvider.GetRequiredService<IPersonQueries>(),
            scope.ServiceProvider.GetRequiredService<IGraphQueries>(),
            new GraphLimits(bansawaliPeople: bansawaliPeople));
        return await service.BansawaliAsync(root, line, As(Maintainer), Cancellation);
    }

    private static async Task<LocalView> LocalViewAsync(
        ServiceProvider services, PersonId centre, int generations, int peopleLimit)
    {
        await using var scope = services.CreateAsyncScope();
        var service = new GraphService(
            scope.ServiceProvider.GetRequiredService<IPersonQueries>(),
            scope.ServiceProvider.GetRequiredService<IGraphQueries>(),
            new GraphLimits(generations, peopleLimit));
        return await service.LocalViewAsync(centre, As(Maintainer), Cancellation);
    }

    private static async Task<Overview> OverviewAsync(ServiceProvider services, int overviewPeople)
    {
        await using var scope = services.CreateAsyncScope();
        var service = new GraphService(
            scope.ServiceProvider.GetRequiredService<IPersonQueries>(),
            scope.ServiceProvider.GetRequiredService<IGraphQueries>(),
            new GraphLimits(overviewPeople: overviewPeople));
        return await service.OverviewAsync(As(Maintainer), Cancellation);
    }

    private static async Task<Connections> ConnectionsAsync(
        ServiceProvider services, PersonId from, PersonId to, int pathSearchPeople = GraphLimits.DefaultPathSearchPeople)
    {
        await using var scope = services.CreateAsyncScope();
        var service = new GraphService(
            scope.ServiceProvider.GetRequiredService<IPersonQueries>(),
            scope.ServiceProvider.GetRequiredService<IGraphQueries>(),
            new GraphLimits(pathSearchPeople: pathSearchPeople));
        return await service.FindConnectionsAsync(from, to, As(Maintainer), Cancellation);
    }

    private static async Task ApproveAsync(ServiceProvider services, LinkId link, AccountId actor)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<RelationshipService>()
            .ApproveAsync(link, LinkEndpoint.Parent, As(actor), Cancellation);
    }

    private static async Task DisagreeAsync(ServiceProvider services, LinkId link, AccountId actor)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<RelationshipService>()
            .DisagreeAsync(link, LinkEndpoint.Parent, As(actor), Cancellation);
    }

    private static async Task SaveAsync(ServiceProvider services, IEnumerable<object> entities)
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
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Great-grandmother → Grandmother → Mother → You → Daughter → Granddaughter, with Mother's
    /// sister Aunt and her child Cousin, and your Sister. All links active.
    /// </summary>
    private sealed record Family(Person You, Person Cousin, IReadOnlyList<object> Entities)
    {
        public static Family Create()
        {
            var greatGrandmother = TestData.NewPerson(Maintainer, "Great-grandmother");
            var grandmother = TestData.NewPerson(Maintainer, "Grandmother");
            var mother = TestData.NewPerson(Maintainer, "Mother");
            var aunt = TestData.NewPerson(Maintainer, "Aunt");
            var you = TestData.NewPerson(Maintainer, "You");
            var sister = TestData.NewPerson(Maintainer, "Sister");
            var cousin = TestData.NewPerson(Maintainer, "Cousin");
            var daughter = TestData.NewPerson(Maintainer, "Daughter");
            var granddaughter = TestData.NewPerson(Maintainer, "Granddaughter");
            object[] people = [greatGrandmother, grandmother, mother, aunt, you, sister, cousin, daughter, granddaughter];
            object[] links =
            [
                TestData.NewLink(greatGrandmother, grandmother, Maintainer),
                TestData.NewLink(grandmother, mother, Maintainer),
                TestData.NewLink(grandmother, aunt, Maintainer),
                TestData.NewLink(mother, you, Maintainer),
                TestData.NewLink(mother, sister, Maintainer),
                TestData.NewLink(aunt, cousin, Maintainer),
                TestData.NewLink(you, daughter, Maintainer),
                TestData.NewLink(daughter, granddaughter, Maintainer),
            ];
            return new Family(you, cousin, [.. people, .. links]);
        }
    }
}
