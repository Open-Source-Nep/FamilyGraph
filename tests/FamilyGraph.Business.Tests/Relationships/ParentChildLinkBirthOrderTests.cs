using FamilyGraph.Business.Audit;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Business.Tests.Relationships;

public class ParentChildLinkBirthOrderTests
{
    private static readonly AccountId Alice = new(Guid.NewGuid());
    private static readonly AccountId Bob = new(Guid.NewGuid());

    private static readonly IReadOnlySet<PersonId> NoParents = new HashSet<PersonId>();

    private static AncestorSnapshot NoAncestors(Person person) => new(person.Id, new HashSet<PersonId>(), isComplete: true);

    private static Person NewPerson(string firstName, AccountId maintainer) =>
        Person.Create(new PersonName(firstName), maintainer);

    /// <summary>Alice maintains the parent and Bob the child, so the link starts pending on the child side.</summary>
    private static (Person Parent, Person Child, ParentChildLink Link) LinkBetweenTwoMaintainers()
    {
        var parent = NewPerson("Hari", Alice);
        var child = NewPerson("Maya", Bob);
        var link = ParentChildLink.Create(parent, child, ParentChildType.Biological, Alice, NoParents, NoAncestors(parent), married: false);
        return (parent, child, link);
    }

    [Fact]
    [Trait("AC", "AC-02.10")]
    public void New_link_is_unordered()
    {
        var (_, _, link) = LinkBetweenTwoMaintainers();

        Assert.Null(link.BirthOrder);
    }

    [Fact]
    [Trait("AC", "AC-02.8")]
    public void Parent_maintainer_sets_and_clears_the_birth_order()
    {
        var (parent, _, link) = LinkBetweenTwoMaintainers();

        link.SetBirthOrder(parent, Alice, 2);
        Assert.Equal(2, link.BirthOrder);

        link.SetBirthOrder(parent, Alice, null);
        Assert.Null(link.BirthOrder);
    }

    [Fact]
    [Trait("AC", "AC-02.9")]
    public void Child_maintainer_cannot_set_the_birth_order()
    {
        var (parent, _, link) = LinkBetweenTwoMaintainers();

        var ex = Assert.Throws<NotParentMaintainerException>(() => link.SetBirthOrder(parent, Bob, 1));

        Assert.Equal("link.birth_order_not_parent_maintainer", ex.Code);
        Assert.Null(link.BirthOrder);
    }

    [Fact]
    [Trait("AC", "AC-02.9")]
    public void Setting_the_birth_order_does_not_change_consent()
    {
        var (parent, child, link) = LinkBetweenTwoMaintainers();

        link.SetBirthOrder(parent, Alice, 1);
        Assert.Equal(EndpointApproval.Pending, link.ChildApproval);
        Assert.False(link.IsActive);

        link.Approve(child, Bob, NoAncestors(parent));
        link.SetBirthOrder(parent, Alice, 3);
        Assert.True(link.IsActive);
    }

    [Fact]
    [Trait("AC", "AC-02.8")]
    public void Inactive_link_can_be_placed()
    {
        var (parent, child, link) = LinkBetweenTwoMaintainers();
        link.Disagree(child, parent, Bob);

        link.SetBirthOrder(parent, Alice, 1);

        Assert.Equal(1, link.BirthOrder);
        Assert.Equal(EndpointApproval.Disagreed, link.ChildApproval);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Birth_order_below_one_is_rejected(int birthOrder)
    {
        var (parent, _, link) = LinkBetweenTwoMaintainers();

        var ex = Assert.Throws<InvalidBirthOrderException>(() => link.SetBirthOrder(parent, Alice, birthOrder));

        Assert.Equal("link.birth_order_invalid", ex.Code);
    }

    [Fact]
    public void Repeating_the_current_birth_order_is_rejected()
    {
        var (parent, _, link) = LinkBetweenTwoMaintainers();
        link.SetBirthOrder(parent, Alice, 1);

        var ex = Assert.Throws<BirthOrderUnchangedException>(() => link.SetBirthOrder(parent, Alice, 1));

        Assert.Equal("link.birth_order_unchanged", ex.Code);
    }

    [Fact]
    public void Deleted_link_rejects_the_birth_order()
    {
        var parent = NewPerson("Hari", Alice);
        var child = NewPerson("Maya", Alice);
        var link = ParentChildLink.Create(parent, child, ParentChildType.Biological, Alice, NoParents, NoAncestors(parent), married: false);
        link.Delete(parent, child, Alice, DateTimeOffset.UnixEpoch);

        var ex = Assert.Throws<LinkDeletedException>(() => link.SetBirthOrder(parent, Alice, 1));

        Assert.Equal("link.deleted", ex.Code);
    }

    [Fact]
    public void Child_endpoint_is_not_accepted_as_the_parent()
    {
        var (_, child, link) = LinkBetweenTwoMaintainers();

        Assert.Throws<ArgumentException>(() => link.SetBirthOrder(child, Bob, 1));
    }

    [Fact]
    [Trait("AC", "AC-02.9")]
    public void Setting_the_birth_order_records_a_profile_event()
    {
        var (parent, _, link) = LinkBetweenTwoMaintainers();
        link.TakeEvents();

        link.SetBirthOrder(parent, Alice, 2);
        link.SetBirthOrder(parent, Alice, null);

        Assert.Equal(
            [
                new LinkBirthOrderChanged(link.Id, parent.Id, 2, Alice),
                new LinkBirthOrderChanged(link.Id, parent.Id, null, Alice),
            ],
            link.PendingEvents);
        Assert.All(link.PendingEvents, e => Assert.Equal(AuditFamily.Profile, e.Family));
    }

    [Fact]
    public void Rejected_birth_order_change_records_nothing()
    {
        var (parent, _, link) = LinkBetweenTwoMaintainers();
        link.TakeEvents();

        Assert.Throws<NotParentMaintainerException>(() => link.SetBirthOrder(parent, Bob, 1));

        Assert.Empty(link.PendingEvents);
    }
}
