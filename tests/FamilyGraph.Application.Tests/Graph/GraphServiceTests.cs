using FamilyGraph.Application.Graph;
using FamilyGraph.Application.People;
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

    private GraphService Service(GraphLimits? limits = null) => new(_store, _graph, limits ?? new GraphLimits());

    [Fact]
    [Trait("AC", "AC-08.4")]
    public async Task Local_view_uses_the_configured_limits_and_returns_the_centre_first()
    {
        var mother = AddPerson("Sita", Bob);
        var you = AddPerson("Maya", Alice);
        AddLink(mother, you, ParentChildType.Adoptive);

        var view = await Service(new GraphLimits(viewGenerations: 3, viewPeople: 7))
            .LocalViewAsync(you, Alice, Cancellation);

        Assert.Equal((3, 7), _graph.LastViewLimits);
        Assert.Equal(you, view.CentreId);
        Assert.Equal(
            [("Maya", 0, true), ("Sita", 1, false)],
            view.People.Select(row => (row.Person.Name.FirstName, row.Generation, row.Person.MaintainedByViewer)));
        Assert.Equal(ParentChildType.Adoptive, Assert.Single(view.Links).Type);
    }

    [Fact]
    public async Task Local_view_of_an_unknown_person_is_not_found()
    {
        await Assert.ThrowsAsync<PersonNotFoundException>(
            () => Service().LocalViewAsync(PersonId.New(), Alice, Cancellation));
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

        var connections = await Service(new GraphLimits(morePaths: 1)).FindConnectionsAsync(you, sister, Alice, Cancellation);

        Assert.Equal(GraphLimits.DefaultPathSearchPeople, _graph.LastSearchLimit);
        Assert.Equal(2, connections.Paths.Count);
        Assert.True(connections.MorePathsExist);
        Assert.True(connections.SearchComplete);
        Assert.All(connections.Paths, path =>
        {
            Assert.Equal("Maya", path.Start.Name.FirstName);
            Assert.Equal([StepRelation.Parent, StepRelation.Child], path.Steps.Select(step => step.Relation));
            Assert.Equal("Rina", path.Steps[^1].Person.Name.FirstName);
        });
    }

    [Fact]
    public async Task No_connection_within_a_cut_search_is_reported_as_incomplete()
    {
        var you = AddPerson("Maya", Alice);
        var stranger = AddPerson("Rina", Bob);
        _graph.SearchHitsLimit = true;

        var connections = await Service().FindConnectionsAsync(you, stranger, Alice, Cancellation);

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

        var connections = await Service().FindConnectionsAsync(you, mother, Alice, Cancellation);

        Assert.Empty(connections.Paths);
        Assert.True(connections.SearchComplete);
    }

    [Fact]
    public async Task Connections_with_an_unknown_person_are_not_found()
    {
        var you = AddPerson("Maya", Alice);

        await Assert.ThrowsAsync<PersonNotFoundException>(
            () => Service().FindConnectionsAsync(you, PersonId.New(), Alice, Cancellation));
    }

    [Fact]
    public void Limits_beyond_their_maximums_are_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GraphLimits(viewGenerations: GraphLimits.MaxViewGenerations + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GraphLimits(viewPeople: GraphLimits.MaxViewPeople + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GraphLimits(pathSearchPeople: 0));
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
