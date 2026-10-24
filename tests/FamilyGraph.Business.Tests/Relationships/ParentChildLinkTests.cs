using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Business.Tests.Relationships;

public class ParentChildLinkTests
{
    private static readonly AccountId Alice = new(Guid.NewGuid());
    private static readonly AccountId Bob = new(Guid.NewGuid());
    private static readonly AccountId Carol = new(Guid.NewGuid());

    private static readonly IReadOnlySet<PersonId> NoParents = new HashSet<PersonId>();

    private static AncestorSnapshot NoAncestors(Person person) => new(person.Id, new HashSet<PersonId>(), isComplete: true);

    private static Person NewPerson(string firstName, AccountId maintainer) =>
        Person.Create(new PersonName(firstName), maintainer);

    [Theory]
    [InlineData(nameof(ParentChildType.Biological))]
    [InlineData(nameof(ParentChildType.Adoptive))]
    [Trait("AC", "AC-02.1")]
    public void Link_of_either_type_is_created_and_keeps_its_type(string typeName)
    {
        var type = ParentChildType.FromName(typeName);
        var parent = NewPerson("Hari", Alice);
        var child = NewPerson("Maya", Alice);

        var link = ParentChildLink.Create(parent, child, type, Alice, NoParents, NoAncestors(parent), married: false);

        Assert.Equal(parent.Id, link.ParentId);
        Assert.Equal(child.Id, link.ChildId);
        Assert.Equal(type, link.Type);
    }

    [Fact]
    [Trait("AC", "AC-02.1")]
    public void Link_is_active_when_one_account_maintains_both_endpoints()
    {
        var parent = NewPerson("Hari", Alice);
        var child = NewPerson("Maya", Alice);

        var link = ParentChildLink.Create(parent, child, ParentChildType.Biological, Alice, NoParents, NoAncestors(parent), married: false);

        Assert.Equal(EndpointApproval.Approved, link.ParentApproval);
        Assert.Equal(EndpointApproval.Approved, link.ChildApproval);
        Assert.True(link.IsActive);
    }

    [Fact]
    [Trait("AC", "AC-02.1")]
    public void Child_side_is_pending_when_the_parent_maintainer_creates_the_link()
    {
        var parent = NewPerson("Hari", Alice);
        var child = NewPerson("Maya", Bob);

        var link = ParentChildLink.Create(parent, child, ParentChildType.Biological, Alice, NoParents, NoAncestors(parent), married: false);

        Assert.Equal(EndpointApproval.Approved, link.ParentApproval);
        Assert.Equal(EndpointApproval.Pending, link.ChildApproval);
        Assert.False(link.IsActive);
    }

    [Fact]
    [Trait("AC", "AC-02.1")]
    public void Parent_side_is_pending_when_the_child_maintainer_creates_the_link()
    {
        var parent = NewPerson("Hari", Alice);
        var child = NewPerson("Maya", Bob);

        var link = ParentChildLink.Create(parent, child, ParentChildType.Adoptive, Bob, NoParents, NoAncestors(parent), married: false);

        Assert.Equal(EndpointApproval.Pending, link.ParentApproval);
        Assert.Equal(EndpointApproval.Approved, link.ChildApproval);
        Assert.False(link.IsActive);
    }

    [Fact]
    public void Someone_who_maintains_neither_endpoint_cannot_create_a_link()
    {
        var parent = NewPerson("Hari", Alice);
        var child = NewPerson("Maya", Bob);

        var ex = Assert.Throws<NotLinkEndpointMaintainerException>(
            () => ParentChildLink.Create(parent, child, ParentChildType.Biological, Carol, NoParents, NoAncestors(parent), married: false));

        Assert.Equal("link.not_endpoint_maintainer", ex.Code);
        Assert.IsAssignableFrom<NotPermittedException>(ex);
    }

    [Fact]
    public void Child_can_have_several_parents_of_either_type()
    {
        var child = NewPerson("Maya", Alice);
        var existingParents = new HashSet<PersonId>();
        ParentChildType[] types =
        [
            ParentChildType.Biological,
            ParentChildType.Biological,
            ParentChildType.Adoptive,
            ParentChildType.Adoptive,
            ParentChildType.Biological,
        ];

        foreach (var type in types)
        {
            var parent = NewPerson("Parent", Alice);
            ParentChildLink.Create(parent, child, type, Alice, existingParents, NoAncestors(parent), married: false);
            existingParents.Add(parent.Id);
        }

        Assert.Equal(types.Length, existingParents.Count);
    }

    [Theory]
    [InlineData(nameof(ParentChildType.Biological))]
    [InlineData(nameof(ParentChildType.Adoptive))]
    public void Second_link_for_the_same_parent_and_child_is_rejected_whatever_its_type(string typeName)
    {
        var type = ParentChildType.FromName(typeName);
        var parent = NewPerson("Hari", Alice);
        var child = NewPerson("Maya", Alice);
        var childParents = new HashSet<PersonId> { parent.Id };

        var ex = Assert.Throws<DuplicateLinkException>(
            () => ParentChildLink.Create(parent, child, type, Alice, childParents, NoAncestors(parent), married: false));

        Assert.Equal("link.duplicate", ex.Code);
        Assert.IsAssignableFrom<ConflictException>(ex);
    }

    [Fact]
    public void Person_cannot_be_their_own_parent()
    {
        var person = NewPerson("Hari", Alice);

        var ex = Assert.Throws<SelfLinkException>(
            () => ParentChildLink.Create(person, person, ParentChildType.Biological, Alice, NoParents, NoAncestors(person), married: false));

        Assert.Equal("link.self_link", ex.Code);
    }

    [Fact]
    [Trait("AC", "AC-02.6")]
    public void Two_person_cycle_is_rejected()
    {
        // Maya is already Hari's parent; Hari cannot become Maya's parent.
        var hari = NewPerson("Hari", Alice);
        var maya = NewPerson("Maya", Alice);
        var hariAncestors = new AncestorSnapshot(hari.Id, new HashSet<PersonId> { maya.Id }, isComplete: true);

        var ex = Assert.Throws<LinkCycleDetectedException>(
            () => ParentChildLink.Create(hari, maya, ParentChildType.Biological, Alice, NoParents, hariAncestors, married: false));

        Assert.Equal("link.cycle_detected", ex.Code);
    }

    [Fact]
    [Trait("AC", "AC-02.6")]
    public void Deeper_cycle_is_rejected()
    {
        // A → B → C exists, so C → A is rejected: A is among C's ancestors.
        var a = NewPerson("A", Alice);
        var b = NewPerson("B", Alice);
        var c = NewPerson("C", Bob);
        var cAncestors = new AncestorSnapshot(c.Id, new HashSet<PersonId> { b.Id, a.Id }, isComplete: true);

        Assert.Throws<LinkCycleDetectedException>(
            () => ParentChildLink.Create(c, a, ParentChildType.Adoptive, Alice, NoParents, cAncestors, married: false));
    }

    [Fact]
    [Trait("AC", "AC-02.7")]
    public void Link_is_rejected_when_the_ancestor_check_reached_its_limit()
    {
        var parent = NewPerson("Hari", Alice);
        var child = NewPerson("Maya", Alice);
        var truncated = new AncestorSnapshot(parent.Id, new HashSet<PersonId> { PersonId.New() }, isComplete: false);

        var ex = Assert.Throws<LinkCycleUnverifiableException>(
            () => ParentChildLink.Create(parent, child, ParentChildType.Biological, Alice, NoParents, truncated, married: false));

        Assert.Equal("link.cycle_unverifiable", ex.Code);
        Assert.Contains("could not be verified as cycle-free", ex.Message);
    }

    [Fact]
    [Trait("AC", "AC-02.6")]
    public void Cycle_found_before_the_limit_is_reported_as_a_cycle()
    {
        var parent = NewPerson("Hari", Alice);
        var child = NewPerson("Maya", Alice);
        var truncated = new AncestorSnapshot(parent.Id, new HashSet<PersonId> { child.Id }, isComplete: false);

        Assert.Throws<LinkCycleDetectedException>(
            () => ParentChildLink.Create(parent, child, ParentChildType.Biological, Alice, NoParents, truncated, married: false));
    }

    [Fact]
    public void Ancestor_snapshot_of_someone_other_than_the_parent_is_rejected()
    {
        var parent = NewPerson("Hari", Alice);
        var child = NewPerson("Maya", Alice);

        Assert.Throws<ArgumentException>(
            () => ParentChildLink.Create(parent, child, ParentChildType.Biological, Alice, NoParents, NoAncestors(child), married: false));
    }

    [Fact]
    public void Empty_link_id_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new LinkId(Guid.Empty));
    }
}
