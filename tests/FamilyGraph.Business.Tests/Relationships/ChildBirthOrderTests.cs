using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Business.Tests.Relationships;

public class ChildBirthOrderTests
{
    private static readonly AccountId Alice = new(Guid.NewGuid());
    private static readonly AccountId Bob = new(Guid.NewGuid());

    private static readonly IReadOnlySet<PersonId> NoParents = new HashSet<PersonId>();

    private static AncestorSnapshot NoAncestors(Person person) => new(person.Id, new HashSet<PersonId>(), isComplete: true);

    private static Person NewPerson(string firstName, AccountId maintainer) =>
        Person.Create(new PersonName(firstName), maintainer);

    private static ParentChildLink Link(Person parent, Person child, AccountId creator) =>
        ParentChildLink.Create(parent, child, ParentChildType.Biological, creator, NoParents, NoAncestors(parent));

    /// <summary>Alice maintains the parent and two children; Bob maintains a third child.</summary>
    private static (Person Parent, ParentChildLink Ram, ParentChildLink Sita, ParentChildLink Gita) ParentWithThreeChildren()
    {
        var parent = NewPerson("Hari", Alice);
        var ram = Link(parent, NewPerson("Ram", Alice), Alice);
        var sita = Link(parent, NewPerson("Sita", Alice), Alice);
        var gita = Link(parent, NewPerson("Gita", Bob), Alice);
        return (parent, ram, sita, gita);
    }

    [Fact]
    [Trait("AC", "AC-02.10")]
    public void New_link_is_unordered()
    {
        var (_, ram, sita, gita) = ParentWithThreeChildren();

        Assert.Null(ram.BirthOrder);
        Assert.Null(sita.BirthOrder);
        Assert.Null(gita.BirthOrder);
    }

    [Fact]
    [Trait("AC", "AC-02.8")]
    public void Parent_maintainer_places_children_eldest_first()
    {
        var (parent, ram, sita, gita) = ParentWithThreeChildren();

        ChildBirthOrder.Arrange(parent, Alice, [ram, sita, gita], [sita.Id, gita.Id, ram.Id]);

        Assert.Equal(1, sita.BirthOrder);
        Assert.Equal(2, gita.BirthOrder);
        Assert.Equal(3, ram.BirthOrder);
    }

    [Fact]
    [Trait("AC", "AC-02.8")]
    public void Children_left_out_of_the_order_become_unordered()
    {
        var (parent, ram, sita, gita) = ParentWithThreeChildren();
        ChildBirthOrder.Arrange(parent, Alice, [ram, sita, gita], [ram.Id, sita.Id, gita.Id]);

        ChildBirthOrder.Arrange(parent, Alice, [ram, sita, gita], [gita.Id]);

        Assert.Equal(1, gita.BirthOrder);
        Assert.Null(ram.BirthOrder);
        Assert.Null(sita.BirthOrder);
    }

    [Fact]
    [Trait("AC", "AC-02.9")]
    public void Only_the_parent_maintainer_can_change_the_order()
    {
        var (parent, ram, sita, gita) = ParentWithThreeChildren();

        var ex = Assert.Throws<NotParentMaintainerException>(
            () => ChildBirthOrder.Arrange(parent, Bob, [ram, sita, gita], [gita.Id]));

        Assert.Equal("link.birth_order_not_parent_maintainer", ex.Code);
        Assert.Null(gita.BirthOrder);
    }

    [Fact]
    [Trait("AC", "AC-02.9")]
    public void Ordering_does_not_change_consent()
    {
        var (parent, ram, sita, gita) = ParentWithThreeChildren();

        ChildBirthOrder.Arrange(parent, Alice, [ram, sita, gita], [gita.Id, ram.Id]);

        Assert.Equal(EndpointApproval.Pending, gita.ChildApproval);
        Assert.False(gita.IsActive);
        Assert.True(ram.IsActive);
    }

    [Fact]
    [Trait("AC", "AC-02.8")]
    public void Pending_and_inactive_children_keep_their_position()
    {
        var (parent, ram, sita, gita) = ParentWithThreeChildren();
        var asha = NewPerson("Asha", Bob);
        var bobChild = Link(parent, asha, Alice);
        bobChild.Disagree(asha, parent, Bob);

        ChildBirthOrder.Arrange(parent, Alice, [ram, sita, gita, bobChild], [bobChild.Id, gita.Id]);

        Assert.Equal(1, bobChild.BirthOrder);
        Assert.Equal(2, gita.BirthOrder);
    }

    [Fact]
    public void Child_not_linked_to_the_parent_is_rejected()
    {
        var (parent, ram, sita, gita) = ParentWithThreeChildren();
        var otherParent = NewPerson("Mohan", Alice);
        var other = Link(otherParent, NewPerson("Kiran", Alice), Alice);

        var ex = Assert.Throws<BirthOrderUnknownChildException>(
            () => ChildBirthOrder.Arrange(parent, Alice, [ram, sita, gita], [ram.Id, other.Id]));

        Assert.Equal("link.birth_order_unknown_child", ex.Code);
        Assert.Null(ram.BirthOrder);
    }

    [Fact]
    public void Child_listed_twice_is_rejected()
    {
        var (parent, ram, sita, gita) = ParentWithThreeChildren();

        var ex = Assert.Throws<BirthOrderDuplicateChildException>(
            () => ChildBirthOrder.Arrange(parent, Alice, [ram, sita, gita], [ram.Id, sita.Id, ram.Id]));

        Assert.Equal("link.birth_order_duplicate_child", ex.Code);
        Assert.Null(ram.BirthOrder);
    }

    [Fact]
    public void Repeating_the_current_order_is_rejected()
    {
        var (parent, ram, sita, gita) = ParentWithThreeChildren();
        ChildBirthOrder.Arrange(parent, Alice, [ram, sita, gita], [ram.Id, sita.Id]);

        var ex = Assert.Throws<BirthOrderUnchangedException>(
            () => ChildBirthOrder.Arrange(parent, Alice, [ram, sita, gita], [ram.Id, sita.Id]));

        Assert.Equal("link.birth_order_unchanged", ex.Code);
    }

    [Fact]
    public void Links_of_another_parent_or_deleted_links_are_a_caller_error()
    {
        var (parent, ram, sita, _) = ParentWithThreeChildren();
        var otherParent = NewPerson("Mohan", Alice);
        var otherChild = NewPerson("Kiran", Alice);
        var other = Link(otherParent, otherChild, Alice);
        Assert.Throws<ArgumentException>(
            () => ChildBirthOrder.Arrange(parent, Alice, [ram, other], [ram.Id]));

        var deletedChild = NewPerson("Asha", Alice);
        var deleted = Link(parent, deletedChild, Alice);
        deleted.Delete(parent, deletedChild, Alice, DateTimeOffset.UnixEpoch);
        Assert.Throws<ArgumentException>(
            () => ChildBirthOrder.Arrange(parent, Alice, [ram, sita, deleted], [ram.Id]));
    }
}
