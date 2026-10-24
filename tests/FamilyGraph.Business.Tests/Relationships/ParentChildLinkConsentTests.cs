using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Business.Tests.Relationships;

public class ParentChildLinkConsentTests
{
    private static readonly AccountId Alice = new(Guid.NewGuid());
    private static readonly AccountId Bob = new(Guid.NewGuid());
    private static readonly AccountId Admin = new(Guid.NewGuid());

    private static readonly IReadOnlySet<PersonId> NoParents = new HashSet<PersonId>();

    private static AncestorSnapshot NoAncestors(Person person) => new(person.Id, new HashSet<PersonId>(), isComplete: true);

    private static Person NewPerson(string firstName, AccountId maintainer) =>
        Person.Create(new PersonName(firstName), maintainer);

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
    [Trait("AC", "AC-03.3")]
    public void Approving_the_pending_side_activates_the_link()
    {
        var parent = NewPerson("Hari", Alice);
        var child = NewPerson("Maya", Bob);
        var link = ParentChildLink.Create(parent, child, ParentChildType.Biological, Alice, NoParents, NoAncestors(parent), married: false);

        link.Approve(child, Bob, NoAncestors(parent));

        Assert.Equal(EndpointApproval.Approved, link.ChildApproval);
        Assert.True(link.IsActive);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("AC", "AC-03.1")]
    public void Disagreement_deactivates_link_immediately(bool parentSideDisagrees)
    {
        var (parent, child, link) = ActiveLinkBetweenTwoMaintainers();

        if (parentSideDisagrees)
        {
            link.Disagree(parent, child, Alice);
            Assert.Equal(EndpointApproval.Disagreed, link.ParentApproval);
        }
        else
        {
            link.Disagree(child, parent, Bob);
            Assert.Equal(EndpointApproval.Disagreed, link.ChildApproval);
        }

        Assert.False(link.IsActive);
    }

    [Fact]
    public void Pending_side_can_decline_the_link()
    {
        var parent = NewPerson("Hari", Alice);
        var child = NewPerson("Maya", Bob);
        var link = ParentChildLink.Create(parent, child, ParentChildType.Adoptive, Alice, NoParents, NoAncestors(parent), married: false);

        link.Disagree(child, parent, Bob);

        Assert.Equal(EndpointApproval.Disagreed, link.ChildApproval);
        Assert.Equal(EndpointApproval.Approved, link.ParentApproval);
        Assert.False(link.IsActive);
    }

    [Fact]
    [Trait("AC", "AC-03.3")]
    public void Withdrawing_disagreement_reactivates_link_without_fresh_confirmation_from_the_other_side()
    {
        var (parent, child, link) = ActiveLinkBetweenTwoMaintainers();
        link.Disagree(child, parent, Bob);

        link.Approve(child, Bob, NoAncestors(parent));

        Assert.Equal(EndpointApproval.Approved, link.ParentApproval);
        Assert.Equal(EndpointApproval.Approved, link.ChildApproval);
        Assert.True(link.IsActive);
    }

    [Fact]
    [Trait("AC", "AC-03.3")]
    public void Link_stays_inactive_until_both_disagreements_are_withdrawn()
    {
        var (parent, child, link) = ActiveLinkBetweenTwoMaintainers();
        link.Disagree(parent, child, Alice);
        link.Disagree(child, parent, Bob);

        link.Approve(child, Bob, NoAncestors(parent));
        Assert.False(link.IsActive);

        link.Approve(parent, Alice, NoAncestors(parent));
        Assert.True(link.IsActive);
    }

    [Fact]
    [Trait("AC", "AC-03.5")]
    public void Someone_who_does_not_maintain_the_endpoint_cannot_approve_it()
    {
        var (parent, child, link) = ActiveLinkBetweenTwoMaintainers();
        link.Disagree(child, parent, Bob);

        // An administrator, or the other endpoint's maintainer, has no authority over Bob's side.
        Assert.Throws<NotConsentMaintainerException>(() => link.Approve(child, Admin, NoAncestors(parent)));
        var ex = Assert.Throws<NotConsentMaintainerException>(() => link.Approve(child, Alice, NoAncestors(parent)));

        Assert.Equal("link.consent_not_maintainer", ex.Code);
        Assert.Equal(EndpointApproval.Disagreed, link.ChildApproval);
        Assert.False(link.IsActive);
    }

    [Fact]
    public void Someone_who_does_not_maintain_the_endpoint_cannot_disagree_for_it()
    {
        var (parent, child, link) = ActiveLinkBetweenTwoMaintainers();

        Assert.Throws<NotConsentMaintainerException>(() => link.Disagree(child, parent, Admin));
        Assert.Throws<NotConsentMaintainerException>(() => link.Disagree(child, parent, Alice));

        Assert.True(link.IsActive);
    }

    [Fact]
    public void Approving_an_approved_side_is_rejected()
    {
        var (parent, child, link) = ActiveLinkBetweenTwoMaintainers();

        var ex = Assert.Throws<ConsentUnchangedException>(() => link.Approve(parent, Alice, NoAncestors(parent)));

        Assert.Equal("link.consent_unchanged", ex.Code);
    }

    [Fact]
    public void Disagreeing_twice_is_rejected()
    {
        var (parent, child, link) = ActiveLinkBetweenTwoMaintainers();
        link.Disagree(child, parent, Bob);

        Assert.Throws<ConsentUnchangedException>(() => link.Disagree(child, parent, Bob));
    }

    [Fact]
    public void Sole_maintainer_of_both_endpoints_cannot_disagree_and_must_delete_instead()
    {
        var parent = NewPerson("Hari", Alice);
        var child = NewPerson("Maya", Alice);
        var link = ParentChildLink.Create(parent, child, ParentChildType.Biological, Alice, NoParents, NoAncestors(parent), married: false);

        var ex = Assert.Throws<SoleMaintainerMustDeleteException>(() => link.Disagree(child, parent, Alice));

        Assert.Equal("link.sole_maintainer_must_delete", ex.Code);
        Assert.True(link.IsActive);
    }

    [Fact]
    [Trait("AC", "AC-02.6")]
    public void Reactivation_into_a_cycle_is_rejected()
    {
        var (parent, child, link) = ActiveLinkBetweenTwoMaintainers();
        link.Disagree(child, parent, Bob);
        var parentAncestors = new AncestorSnapshot(parent.Id, new HashSet<PersonId> { child.Id }, isComplete: true);

        Assert.Throws<LinkCycleDetectedException>(() => link.Approve(child, Bob, parentAncestors));

        Assert.Equal(EndpointApproval.Disagreed, link.ChildApproval);
        Assert.False(link.IsActive);
    }

    [Fact]
    [Trait("AC", "AC-02.7")]
    public void Reactivation_is_rejected_when_the_ancestor_check_reached_its_limit()
    {
        var (parent, child, link) = ActiveLinkBetweenTwoMaintainers();
        link.Disagree(child, parent, Bob);
        var truncated = new AncestorSnapshot(parent.Id, new HashSet<PersonId>(), isComplete: false);

        Assert.Throws<LinkCycleUnverifiableException>(() => link.Approve(child, Bob, truncated));
        Assert.False(link.IsActive);
    }

    [Fact]
    public void Approval_that_leaves_the_link_inactive_skips_the_cycle_check()
    {
        var (parent, child, link) = ActiveLinkBetweenTwoMaintainers();
        link.Disagree(parent, child, Alice);
        link.Disagree(child, parent, Bob);
        var truncated = new AncestorSnapshot(parent.Id, new HashSet<PersonId>(), isComplete: false);

        link.Approve(child, Bob, truncated);

        Assert.Equal(EndpointApproval.Approved, link.ChildApproval);
        Assert.False(link.IsActive);
    }

    [Fact]
    public void Person_who_is_not_on_the_link_is_rejected()
    {
        var (parent, _, link) = ActiveLinkBetweenTwoMaintainers();
        var stranger = NewPerson("Sita", Bob);

        Assert.Throws<ArgumentException>(() => link.Approve(stranger, Bob, NoAncestors(parent)));
        Assert.Throws<ArgumentException>(() => link.Disagree(stranger, parent, Bob));
    }

    [Fact]
    public void Endpoint_approval_values_are_stable_for_persistence()
    {
        Assert.Equal([1, 2, 3], EndpointApproval.All.Select(approval => approval.Value));
        Assert.Equal(EndpointApproval.Disagreed, EndpointApproval.FromValue(3));
    }
}
