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

    [Fact]
    [Trait("AC", "AC-08.9")]
    public async Task Views_overview_and_paths_walk_marriages_in_the_same_generation()
    {
        var you = AddPerson("Maya", Alice);
        var husband = AddPerson("Ram", Alice);
        var fatherInLaw = AddPerson("Dasharath", Alice);
        AddLink(fatherInLaw, husband);
        AddMarriage(you, husband);

        var view = await Service().LocalViewAsync(you, As(Alice), Cancellation);
        var overview = await Service().OverviewAsync(As(Alice), Cancellation);
        var connections = await Service().FindConnectionsAsync(you, fatherInLaw, As(Alice), Cancellation);

        Assert.Equal(
            [("Maya", 0), ("Ram", 0), ("Dasharath", 1)],
            view.People.Select(row => (row.Person.Name!.FirstName, row.Generation)));
        Assert.Single(view.Marriages);
        Assert.Single(overview.Marriages);
        var path = Assert.Single(connections.Paths);
        Assert.Equal([StepRelation.Spouse, StepRelation.Parent], path.Steps.Select(step => step.Relation));
    }

    [Fact]
    [Trait("AC", "AC-08.21")]
    [Trait("AC", "AC-08.22")]
    public async Task Bansawali_follows_biological_sons_and_lists_the_other_children_without_their_descendants()
    {
        var family = AddFamily();

        var bansawali = await Service().BansawaliAsync(family.Hari, BansawaliLine.Sons, As(Alice), Cancellation);

        Assert.Equal(family.Hari, bansawali.RootId);
        Assert.Equal(BansawaliLine.Sons, bansawali.Line);
        Assert.Equal(
            [("Hari", 0, true), ("Krishna", 1, false), ("Ram", 1, true), ("Rina", 1, false), ("Sita", 1, false), ("Shyam", 2, true)],
            bansawali.People
                .Select(row => (row.Person.Name!.FirstName, row.Generation, row.Continues))
                .OrderBy(row => row.Generation).ThenBy(row => row.FirstName));
        Assert.Equal(5, bansawali.Links.Count);
        Assert.Equal(ParentChildType.Adoptive, bansawali.Links.Single(link => link.ChildId == family.Krishna).Type);
        Assert.All(bansawali.People, row => Assert.False(row.HasMore));
        Assert.False(bansawali.IsTruncated);
    }

    [Fact]
    [Trait("AC", "AC-08.21")]
    public async Task Bansawali_of_all_children_follows_every_line()
    {
        var family = AddFamily();

        var bansawali = await Service().BansawaliAsync(family.Hari, BansawaliLine.AllChildren, As(Alice), Cancellation);

        Assert.Equal(
            ["Hari", "Krishna", "Ram", "Rina", "Sita", "Gita", "Mohan", "Shyam", "Tara"],
            bansawali.People.OrderBy(row => row.Generation).ThenBy(row => row.Person.Name!.FirstName).Select(row => row.Person.Name!.FirstName));
        Assert.All(bansawali.People, row => Assert.True(row.Continues));
    }

    [Fact]
    [Trait("AC", "AC-08.24")]
    [Trait("AC", "AC-05.2")]
    public async Task Bansawali_stops_at_a_private_son_for_others_but_not_for_his_maintainer()
    {
        var hari = AddPerson("Hari", Bob, Gender.Male);
        var ram = AddPerson("Ram", Bob, Gender.Male);
        var shyam = AddPerson("Shyam", Bob, Gender.Male);
        AddLink(hari, ram);
        AddLink(ram, shyam);
        _store.People[ram].SetVisibility(ProfileVisibility.Private, Bob);

        var forOthers = await Service().BansawaliAsync(hari, BansawaliLine.Sons, As(Alice), Cancellation);
        var forMaintainer = await Service().BansawaliAsync(hari, BansawaliLine.Sons, As(Bob), Cancellation);
        var withToggle = await Service().BansawaliAsync(hari, BansawaliLine.AllChildren, As(Alice), Cancellation);

        Assert.Equal(
            [(hari, null, true), (ram, PersonPlaceholder.Private, false)],
            forOthers.People.Select(row => (row.Person.Id, row.Person.Placeholder, row.Continues)));
        Assert.Null(forOthers.People[1].Person.Gender);
        Assert.Equal([hari, ram, shyam], forMaintainer.People.Select(row => row.Person.Id));
        Assert.Equal([hari, ram, shyam], withToggle.People.Select(row => row.Person.Id));
    }

    [Fact]
    [Trait("AC", "AC-08.23")]
    public async Task Bansawali_lists_the_marriages_of_its_people_and_their_spouses()
    {
        var hari = AddPerson("Hari", Alice, Gender.Male);
        var ram = AddPerson("Ram", Alice, Gender.Male);
        var gita = AddPerson("Gita", Alice, Gender.Female);
        var maya = AddPerson("Maya", Alice, Gender.Female);
        var mayaChild = AddPerson("Asha", Alice, Gender.Female);
        AddLink(hari, ram);
        AddLink(maya, mayaChild);
        AddMarriage(hari, gita);
        AddMarriage(ram, maya);
        _store.Marriages.Values
            .Single(marriage => marriage.FirstSpouseId == maya || marriage.SecondSpouseId == maya)
            .SetFormer(_store.People[ram], _store.People[maya], Alice, isFormer: true);

        var bansawali = await Service().BansawaliAsync(hari, BansawaliLine.Sons, As(Alice), Cancellation);

        Assert.Equal([hari, ram], bansawali.People.Select(row => row.Person.Id));
        Assert.Equal(["Gita", "Maya"], bansawali.Spouses.Select(spouse => spouse.Name!.FirstName).Order());
        Assert.Equal(2, bansawali.Marriages.Count);
        Assert.True(bansawali.Marriages.Single(marriage => marriage.SecondSpouseId == maya || marriage.FirstSpouseId == maya).IsFormer);
    }

    [Fact]
    [Trait("AC", "AC-08.21")]
    public async Task Bansawali_leaves_out_a_generation_past_the_limit_and_marks_whose_children_were_cut_off()
    {
        var family = AddFamily();

        var bansawali = await Service(new GraphLimits(bansawaliPeople: 5))
            .BansawaliAsync(family.Hari, BansawaliLine.AllChildren, As(Alice), Cancellation);

        Assert.True(bansawali.IsTruncated);
        Assert.All(bansawali.People, row => Assert.True(row.Generation < 2));
        Assert.Equal(
            ["Krishna", "Ram", "Rina", "Sita"],
            bansawali.People.Where(row => row.HasMore).Select(row => row.Person.Name!.FirstName).Order());
    }

    [Fact]
    public async Task Bansawali_of_an_unknown_person_is_not_found()
    {
        await Assert.ThrowsAsync<PersonNotFoundException>(
            () => Service().BansawaliAsync(PersonId.New(), BansawaliLine.Sons, As(Alice), Cancellation));
    }

    /// <summary>
    /// Hari's children: Ram (son), Sita (daughter, mother of Gita), Krishna (adopted son, father of
    /// Mohan) and Rina (gender not set, mother of Tara). Ram is the father of Shyam.
    /// </summary>
    private (PersonId Hari, PersonId Krishna) AddFamily()
    {
        var hari = AddPerson("Hari", Alice, Gender.Male);
        var ram = AddPerson("Ram", Alice, Gender.Male);
        var sita = AddPerson("Sita", Alice, Gender.Female);
        var krishna = AddPerson("Krishna", Alice, Gender.Male);
        var rina = AddPerson("Rina", Alice);
        AddLink(hari, ram);
        AddLink(hari, sita);
        AddLink(hari, krishna, ParentChildType.Adoptive);
        AddLink(hari, rina);
        AddLink(ram, AddPerson("Shyam", Alice, Gender.Male));
        AddLink(sita, AddPerson("Gita", Alice, Gender.Female));
        AddLink(krishna, AddPerson("Mohan", Alice, Gender.Male));
        AddLink(rina, AddPerson("Tara", Alice));
        return (hari, krishna);
    }

    private void AddMarriage(PersonId spouseId, PersonId otherSpouseId)
    {
        var spouse = _store.People[spouseId];
        var marriage = Marriage.Create(spouse, _store.People[otherSpouseId], spouse.MaintainerId, alreadyMarried: false, directlyRelated: false);
        marriage.TakeEvents();
        _store.Marriages[marriage.Id] = marriage;
    }

    private PersonId AddPerson(string firstName, AccountId maintainer, Gender? gender = null)
    {
        var person = Person.Create(new PersonName(firstName), maintainer);
        person.SetGender(gender, maintainer);
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
            new AncestorSnapshot(parentId, new HashSet<PersonId>(), isComplete: true), married: false);
        if (!link.IsActive)
        {
            link.Approve(child, child.MaintainerId, new AncestorSnapshot(parentId, new HashSet<PersonId>(), isComplete: true));
        }

        _store.Links[link.Id] = link;
        return link;
    }
}
