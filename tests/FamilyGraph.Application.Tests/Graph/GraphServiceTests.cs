using FamilyGraph.Application.Graph;
using FamilyGraph.Application.People;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Application.Tests.People;
using FamilyGraph.Business.Graph;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Application.Tests.Graph;

public class GraphServiceTests
{
    private static readonly AccountId Alice = new(Guid.NewGuid());
    private static readonly AccountId Bob = new(Guid.NewGuid());

    private readonly InMemoryPeople _store = new();
    private readonly InMemoryGraph _graph;

    public GraphServiceTests() => _graph = new InMemoryGraph(_store);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static Viewer As(AccountId account) => new(account, IsAdministrator: false);

    private GraphService Service(GraphLimits? limits = null) => new(_store, _graph, limits ?? new GraphLimits());

    [Fact]
    [Trait("AC", "AC-08.4")]
    public async Task Local_view_uses_the_configured_limits_and_returns_the_centre_first()
    {
        var mother = AddPerson("Sita", Bob);
        var you = AddPerson("Maya", Alice);
        AddLink(mother, you, ParentChildType.Adoptive);

        var view = await Service(new GraphLimits(viewGenerations: 3, viewPeople: 7))
            .LocalViewAsync(you, As(Alice), Cancellation);

        Assert.Equal((3, 7), _graph.LastViewLimits);
        Assert.Equal(you, view.CentreId);
        Assert.Equal(
            [("Maya", 0, true), ("Sita", 1, false)],
            view.People.Select(row => (row.Person.Name!.FirstName, row.Generation, row.Person.MaintainedByViewer)));
        Assert.Equal(ParentChildType.Adoptive, Assert.Single(view.Links).Type);
    }

    [Fact]
    [Trait("AC", "AC-05.2")]
    [Trait("AC", "AC-05.4")]
    public async Task Views_and_paths_pass_through_a_private_placeholder()
    {
        var grandmother = AddPerson("Gita", Bob);
        var mother = AddPerson("Sita", Bob);
        var you = AddPerson("Maya", Alice);
        AddLink(grandmother, mother);
        AddLink(mother, you);
        _store.People[mother].SetVisibility(ProfileVisibility.Private, Bob);

        var view = await Service().LocalViewAsync(you, As(Alice), Cancellation);
        var connections = await Service().FindConnectionsAsync(you, grandmother, As(Alice), Cancellation);

        Assert.Equal(
            [(you, "Maya"), (mother, null), (grandmother, "Gita")],
            view.People.Select(row => (row.Person.Id, row.Person.Name?.FirstName)));
        Assert.Equal(PersonPlaceholder.Private, view.People[1].Person.Placeholder);
        Assert.Equal(2, view.Links.Count);
        var path = Assert.Single(connections.Paths);
        Assert.Equal(
            [(PersonPlaceholder.Private, null), (null, "Gita")],
            path.Steps.Select(step => (step.Person.Placeholder, step.Person.Name?.FirstName)));
    }

    [Fact]
    [Trait("AC", "AC-05.3")]
    [Trait("AC", "AC-05.4")]
    public async Task Paths_pass_through_a_deleted_placeholder()
    {
        var grandmother = AddPerson("Gita", Alice);
        var mother = AddPerson("Sita", Alice);
        var you = AddPerson("Maya", Alice);
        AddLink(grandmother, mother);
        AddLink(mother, you);
        _store.People[mother].Delete(Alice, actorIsAdministrator: false, DateTimeOffset.UtcNow);

        var connections = await Service().FindConnectionsAsync(you, grandmother, As(Alice), Cancellation);

        Assert.Equal(
            [PersonPlaceholder.Deleted, null],
            Assert.Single(connections.Paths).Steps.Select(step => step.Person.Placeholder));
    }

    [Fact]
    public async Task Local_view_of_an_unknown_person_is_not_found()
    {
        await Assert.ThrowsAsync<PersonNotFoundException>(
            () => Service().LocalViewAsync(PersonId.New(), As(Alice), Cancellation));
    }

    [Fact]
    [Trait("AC", "AC-08.6")]
    public async Task Connections_list_the_shortest_path_then_the_configured_number_of_further_paths()
    {
        // Three shared parents give three two-step paths between the siblings.
        var you = AddPerson("Maya", Alice);
        var sister = AddPerson("Rina", Alice);
        foreach (var name in new[] { "Sita", "Hari", "Gita" })
        {
            var parent = AddPerson(name, Alice);
            AddLink(parent, you);
            AddLink(parent, sister);
        }

        var connections = await Service(new GraphLimits(morePaths: 1)).FindConnectionsAsync(you, sister, As(Alice), Cancellation);

        Assert.Equal(GraphLimits.DefaultPathSearchPeople, _graph.LastSearchLimit);
        Assert.Equal(2, connections.Paths.Count);
        Assert.True(connections.MorePathsExist);
        Assert.True(connections.SearchComplete);
        Assert.All(connections.Paths, path =>
        {
            Assert.Equal("Maya", path.Start.Name!.FirstName);
            Assert.Equal([StepRelation.Parent, StepRelation.Child], path.Steps.Select(step => step.Relation));
            Assert.Equal("Rina", path.Steps[^1].Person.Name!.FirstName);
        });
    }

    [Fact]
    public async Task No_connection_within_a_cut_search_is_reported_as_incomplete()
    {
        var you = AddPerson("Maya", Alice);
        var stranger = AddPerson("Rina", Bob);
        _graph.SearchHitsLimit = true;

        var connections = await Service().FindConnectionsAsync(you, stranger, As(Alice), Cancellation);

        Assert.Empty(connections.Paths);
        Assert.False(connections.MorePathsExist);
        Assert.False(connections.SearchComplete);
    }

    [Fact]
    [Trait("AC", "AC-08.1")]
    public async Task Connections_use_only_active_links()
    {
        var you = AddPerson("Maya", Alice);
        var mother = AddPerson("Sita", Bob);
        var link = AddLink(mother, you);
        link.Disagree(_store.People[mother], _store.People[you], Bob);

        var connections = await Service().FindConnectionsAsync(you, mother, As(Alice), Cancellation);

        Assert.Empty(connections.Paths);
        Assert.True(connections.SearchComplete);
    }

    [Fact]
    public async Task Connections_with_an_unknown_person_are_not_found()
    {
        var you = AddPerson("Maya", Alice);

        await Assert.ThrowsAsync<PersonNotFoundException>(
            () => Service().FindConnectionsAsync(you, PersonId.New(), As(Alice), Cancellation));
    }

    [Fact]
    [Trait("AC", "AC-08.4")]
    public async Task Overview_shows_everyone_connected_to_your_profiles_up_to_the_configured_limit()
    {
        var mother = AddPerson("Sita", Bob);
        var you = AddPerson("Maya", Alice);
        var daughter = AddPerson("Rina", Bob);
        var other = AddPerson("Anil", Alice);
        _ = AddPerson("Hari", Bob);
        AddLink(mother, you);
        AddLink(you, daughter, ParentChildType.Adoptive);

        var overview = await Service(new GraphLimits(overviewPeople: 4)).OverviewAsync(As(Alice), Cancellation);

        Assert.Equal(4, _graph.LastOverviewLimit);
        Assert.Equal(
            ["Anil", "Maya", "Sita", "Rina"],
            overview.People.Select(person => person.Name!.FirstName));
        Assert.Equal([true, true, false, false], overview.People.Select(person => person.MaintainedByViewer));
        Assert.Equal(2, overview.Links.Count);
        Assert.False(overview.IsTruncated);

        var cut = await Service(new GraphLimits(overviewPeople: 2)).OverviewAsync(As(Alice), Cancellation);

        Assert.Equal(2, cut.People.Count);
        Assert.True(cut.IsTruncated);
    }

    [Fact]
    public async Task Overview_without_profiles_is_empty()
    {
        _ = AddPerson("Hari", Bob);

        var overview = await Service().OverviewAsync(As(Alice), Cancellation);

        Assert.Empty(overview.People);
        Assert.Empty(overview.Links);
        Assert.False(overview.IsTruncated);
    }

    [Fact]
    [Trait("AC", "AC-08.1")]
    public async Task Overview_uses_only_active_links()
    {
        var you = AddPerson("Maya", Alice);
        var mother = AddPerson("Sita", Bob);
        var link = AddLink(mother, you);
        link.Disagree(_store.People[mother], _store.People[you], Bob);

        var overview = await Service().OverviewAsync(As(Alice), Cancellation);

        Assert.Equal(["Maya"], overview.People.Select(person => person.Name!.FirstName));
        Assert.Empty(overview.Links);
    }

    [Fact]
    public void Limits_beyond_their_maximums_are_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GraphLimits(viewGenerations: GraphLimits.MaxViewGenerations + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GraphLimits(viewPeople: GraphLimits.MaxViewPeople + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GraphLimits(pathSearchPeople: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GraphLimits(overviewPeople: GraphLimits.MaxOverviewPeople + 1));
    }

    private PersonId AddPerson(string firstName, AccountId maintainer)
    {
        var person = Person.Create(new PersonName(firstName), maintainer);
        person.TakeEvents();
        _store.People[person.Id] = person;
        return person.Id;
    }

    /// <summary>An active link, approved for both endpoints.</summary>
    private ParentChildLink AddLink(PersonId parentId, PersonId childId, ParentChildType? type = null)
    {
        var parent = _store.People[parentId];
        var child = _store.People[childId];
        var link = ParentChildLink.Create(
            parent,
            child,
            type ?? ParentChildType.Biological,
            parent.MaintainerId,
            new HashSet<PersonId>(),
            new AncestorSnapshot(parentId, new HashSet<PersonId>(), isComplete: true));
        if (!link.IsActive)
        {
            link.Approve(child, child.MaintainerId, new AncestorSnapshot(parentId, new HashSet<PersonId>(), isComplete: true));
        }

        _store.Links[link.Id] = link;
        return link;
    }
}
