using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Business.Tests.Relationships;

public class ParentChildLinkRemovalTests
{
    private static readonly AccountId Alice = new(Guid.NewGuid());
    private static readonly AccountId Bob = new(Guid.NewGuid());
    private static readonly AccountId Admin = new(Guid.NewGuid());

    private static readonly IReadOnlySet<PersonId> NoParents = new HashSet<PersonId>();

    private static readonly DateTimeOffset Now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private static AncestorSnapshot NoAncestors(Person person) => new(person.Id, new HashSet<PersonId>(), isComplete: true);

    private static Person NewPerson(string firstName, AccountId maintainer) =>
        Person.Create(new PersonName(firstName), maintainer);

    private static (Person Parent, Person Child, ParentChildLink Link) LinkMaintainedByAlice()
    {
        var parent = NewPerson("Hari", Alice);
        var child = NewPerson("Maya", Alice);
        var link = ParentChildLink.Create(parent, child, ParentChildType.Biological, Alice, NoParents, NoAncestors(parent), married: false);
        return (parent, child, link);
    }

    /// <summary>Alice maintains the parent, Bob the child, and both have approved.</summary>
    private static (Person Parent, Person Child, ParentChildLink Link) ActiveLinkBetweenTwoMaintainers()
    {
        var parent = NewPerson("Hari", Alice);
        var child = NewPerson("Maya", Bob);
        var link = ParentChildLink.Create(parent, child, ParentChildType.Biological, Alice, NoParents, NoAncestors(parent), married: false);
        link.Approve(child, Bob, NoAncestors(parent));
        return (parent, child, link);
    }

    [Fact]
    [Trait("AC", "AC-03.4")]
    public void Sole_maintainer_deletes_the_link_and_it_leaves_the_active_graph()
    {
        var (parent, child, link) = LinkMaintainedByAlice();

        link.Delete(parent, child, Alice, Now);

        Assert.True(link.IsDeleted);
        Assert.Equal(Now, link.DeletedAt);
        Assert.False(link.IsActive);
    }

    [Fact]
    [Trait("AC", "AC-03.4")]
    public void Deletion_keeps_the_link_and_its_consent_state_for_history()
    {
        var (parent, child, link) = LinkMaintainedByAlice();

        link.Delete(parent, child, Alice, Now);

        Assert.Equal(parent.Id, link.ParentId);
        Assert.Equal(child.Id, link.ChildId);
        Assert.Equal(ParentChildType.Biological, link.Type);
        Assert.Equal(EndpointApproval.Approved, link.ParentApproval);
        Assert.Equal(EndpointApproval.Approved, link.ChildApproval);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Maintainer_of_only_one_endpoint_cannot_delete(bool parentSideTries)
    {
        var (parent, child, link) = ActiveLinkBetweenTwoMaintainers();

        var ex = Assert.Throws<NotSoleMaintainerException>(
            () => link.Delete(parent, child, parentSideTries ? Alice : Bob, Now));

        Assert.Equal("link.not_sole_maintainer", ex.Code);
        Assert.False(link.IsDeleted);
        Assert.True(link.IsActive);
    }

    [Fact]
    [Trait("AC", "AC-03.5")]
    public void Someone_who_maintains_neither_endpoint_cannot_delete()
    {
        var (parent, child, link) = LinkMaintainedByAlice();

        Assert.Throws<NotSoleMaintainerException>(() => link.Delete(parent, child, Admin, Now));

        Assert.False(link.IsDeleted);
    }

    [Fact]
    public void Maintainers_on_different_accounts_remove_the_link_by_withdrawing_approval()
    {
        var (parent, child, link) = ActiveLinkBetweenTwoMaintainers();

        link.Disagree(parent, child, Alice);

        Assert.False(link.IsActive);
        Assert.False(link.IsDeleted);
        Assert.Equal(EndpointApproval.Approved, link.ChildApproval);
    }

    [Fact]
    [Trait("AC", "AC-03.3")]
    public void Withdrawn_approval_can_be_restored_while_the_other_approval_is_valid()
    {
        var (parent, child, link) = ActiveLinkBetweenTwoMaintainers();
        link.Disagree(parent, child, Alice);

        link.Approve(parent, Alice, NoAncestors(parent));

        Assert.True(link.IsActive);
    }

    [Fact]
    public void Deleting_twice_is_rejected()
    {
        var (parent, child, link) = LinkMaintainedByAlice();
        link.Delete(parent, child, Alice, Now);

        var ex = Assert.Throws<LinkDeletedException>(() => link.Delete(parent, child, Alice, Now.AddMinutes(1)));

        Assert.Equal("link.deleted", ex.Code);
        Assert.Equal(Now, link.DeletedAt);
    }

    [Fact]
    public void Deleted_link_cannot_be_approved_or_disagreed_with()
    {
        var (parent, child, link) = LinkMaintainedByAlice();
        link.Delete(parent, child, Alice, Now);

        Assert.Throws<LinkDeletedException>(() => link.Approve(parent, Alice, NoAncestors(parent)));
        Assert.Throws<LinkDeletedException>(() => link.Disagree(parent, child, Alice));
        Assert.False(link.IsActive);
    }

    [Fact]
    public void Person_who_is_not_on_the_link_is_rejected()
    {
        var (parent, child, link) = LinkMaintainedByAlice();
        var stranger = NewPerson("Sita", Alice);

        Assert.Throws<ArgumentException>(() => link.Delete(stranger, child, Alice, Now));
        Assert.Throws<ArgumentException>(() => link.Delete(child, parent, Alice, Now));
        Assert.False(link.IsDeleted);
    }
}
