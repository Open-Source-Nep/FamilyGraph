using FamilyGraph.Business.Graph;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Business.Tests.Graph;

public class RelationshipPathsTests
{
    private readonly PersonId _you = PersonId.New();
    private readonly PersonId _mother = PersonId.New();
    private readonly PersonId _father = PersonId.New();
    private readonly PersonId _sister = PersonId.New();
    private readonly PersonId _grandmother = PersonId.New();

    [Fact]
    [Trait("AC", "AC-08.2")]
    public void Shortest_path_goes_up_to_the_parent_and_down_to_the_sibling()
    {
        var links = new[] { Link(_mother, _you), Link(_mother, _sister), Link(_grandmother, _mother) };

        var path = Assert.Single(RelationshipPaths.Find(_you, _sister, links, maxPaths: 5));

        Assert.Equal(_you, path.Start);
        Assert.Equal(
            [(StepRelation.Parent, _mother), (StepRelation.Child, _sister)],
            path.Steps.Select(step => (step.Relation, step.PersonId)));
        Assert.Equal(_sister, path.End);
    }

    [Fact]
    [Trait("AC", "AC-08.6")]
    public void Further_paths_follow_the_shortest_in_order_of_length()
    {
        // Full siblings: two two-step paths. The grandmother adds a longer one via a second marriage.
        var halfUncle = PersonId.New();
        var links = new[]
        {
            Link(_mother, _you), Link(_father, _you), Link(_mother, _sister), Link(_father, _sister),
            Link(_grandmother, _mother), Link(_grandmother, halfUncle), Link(halfUncle, _sister),
        };

        var paths = RelationshipPaths.Find(_you, _sister, links, maxPaths: 5);

        Assert.Equal([2, 2, 4], paths.Select(path => path.Steps.Count));
        Assert.Equal(
            [_mother, _father],
            paths.Take(2).Select(path => path.Steps[0].PersonId).OrderBy(id => id == _father));
        Assert.All(paths, path => Assert.Equal(path.People.Count(), path.People.Distinct().Count()));
    }

    [Fact]
    [Trait("AC", "AC-08.6")]
    public void Paths_stop_at_the_requested_number()
    {
        var links = new[] { Link(_mother, _you), Link(_father, _you), Link(_mother, _sister), Link(_father, _sister) };

        Assert.Single(RelationshipPaths.Find(_you, _sister, links, maxPaths: 1));
    }

    [Fact]
    [Trait("AC", "AC-08.7")]
    public void Adoptive_steps_keep_their_type_and_flag_the_path()
    {
        var links = new[] { Link(_mother, _you, ParentChildType.Adoptive), Link(_mother, _sister) };

        var path = Assert.Single(RelationshipPaths.Find(_sister, _you, links, maxPaths: 5));

        Assert.Equal(
            [(StepRelation.Parent, ParentChildType.Biological), (StepRelation.Child, ParentChildType.Adoptive)],
            path.Steps.Select(step => (step.Relation, step.Link.Type)));
        Assert.True(path.CrossesAdoption);
    }

    [Fact]
    public void No_path_when_the_links_do_not_connect_the_people()
    {
        var links = new[] { Link(_mother, _you), Link(_father, _sister) };

        Assert.Empty(RelationshipPaths.Find(_you, _sister, links, maxPaths: 5));
    }

    [Fact]
    public void Path_to_yourself_has_no_steps()
    {
        var path = Assert.Single(RelationshipPaths.Find(_you, _you, [], maxPaths: 5));

        Assert.Empty(path.Steps);
        Assert.Equal(_you, path.End);
    }

    [Fact]
    public void A_cycle_in_the_links_is_tolerated_and_no_path_repeats_a_person()
    {
        // Cycles are rejected when links are created (PRD 02), but traversal must still tolerate one.
        var links = new[] { Link(_mother, _you), Link(_you, _grandmother), Link(_grandmother, _mother), Link(_mother, _sister) };

        var paths = RelationshipPaths.Find(_you, _sister, links, maxPaths: 5);

        Assert.Equal([2, 3], paths.Select(path => path.Steps.Count));
        Assert.All(paths, path => Assert.Equal(path.People.Count(), path.People.Distinct().Count()));
    }

    [Fact]
    public void Result_does_not_depend_on_the_order_of_the_links()
    {
        var links = new[] { Link(_mother, _you), Link(_father, _you), Link(_mother, _sister), Link(_father, _sister) };

        var forward = RelationshipPaths.Find(_you, _sister, links, maxPaths: 5);
        var backward = RelationshipPaths.Find(_you, _sister, links.Reverse(), maxPaths: 5);

        Assert.Equal(
            forward.Select(path => path.Steps.Select(step => step.Link.Id).ToList()),
            backward.Select(path => path.Steps.Select(step => step.Link.Id).ToList()));
    }

    private static GraphLink Link(PersonId parent, PersonId child, ParentChildType? type = null) =>
        new(LinkId.New(), parent, child, type ?? ParentChildType.Biological);
}
