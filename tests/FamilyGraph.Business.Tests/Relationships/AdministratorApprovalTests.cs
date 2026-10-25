using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Business.Tests.Relationships;

/// <summary>An administrator approving a pending side on behalf of its maintainer (D-040).</summary>
public class AdministratorApprovalTests
{
    private static readonly AccountId Alice = new(Guid.NewGuid());
    private static readonly AccountId Bob = new(Guid.NewGuid());
    private static readonly AccountId Admin = new(Guid.NewGuid());

    private static readonly IReadOnlySet<PersonId> NoParents = new HashSet<PersonId>();

    private static readonly DateTimeOffset Now = new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);

    private static AncestorSnapshot NoAncestors(Person person) => new(person.Id, new HashSet<PersonId>(), isComplete: true);

    private static Person NewPerson(string firstName, AccountId maintainer) =>
        Person.Create(new PersonName(firstName), maintainer);

    /// <summary>Alice created the link from the parent, so Bob's child side is pending.</summary>
    private static (Person Parent, Person Child, ParentChildLink Link) PendingLink()
    {
        var parent = NewPerson("Hari", Alice);
        var child = NewPerson("Maya", Bob);
        var link = ParentChildLink.Create(parent, child, ParentChildType.Biological, Alice, NoParents, NoAncestors(parent), married: false);
        link.TakeEvents();
        return (parent, child, link);
    }

    /// <summary>Alice created the marriage from Ram, so Bob's side for Sita is pending.</summary>
    private static (Person Ram, Person Sita, Marriage Marriage) PendingMarriage()
    {
        var ram = NewPerson("Ram", Alice);
        var sita = NewPerson("Sita", Bob);
        var marriage = Marriage.Create(ram, sita, Alice, alreadyMarried: false, directlyRelated: false);
        marriage.TakeEvents();
        return (ram, sita, marriage);
    }

    [Fact]
    [Trait("AC", "AC-03.6")]
    [Trait("AC", "AC-03.8")]
    public void An_administrator_approves_a_pending_side_and_the_link_becomes_active()
    {
        var (parent, child, link) = PendingLink();

        link.ApproveAsAdministrator(child, Admin, actorIsAdministrator: true, NoAncestors(parent));

        Assert.Equal(EndpointApproval.Approved, link.ChildApproval);
        Assert.True(link.IsActive);
        Assert.Equal([new LinkEndpointApprovedByAdministrator(link.Id, child.Id, Admin, Bob)], link.PendingEvents);
        Assert.True(child.IsMaintainedBy(Bob));
    }

    [Fact]
    [Trait("AC", "AC-03.6")]
    public void An_administrator_approval_waits_for_the_other_side()
    {
        var parent = NewPerson("Hari", Alice);
        var child = NewPerson("Maya", Bob);
        var link = ParentChildLink.Create(parent, child, ParentChildType.Biological, Bob, NoParents, NoAncestors(parent), married: false);
        link.Disagree(child, parent, Bob);
        link.TakeEvents();

        link.ApproveAsAdministrator(parent, Admin, actorIsAdministrator: true, NoAncestors(parent));

        Assert.Equal(EndpointApproval.Approved, link.ParentApproval);
        Assert.False(link.IsActive);
    }

    [Fact]
    [Trait("AC", "AC-03.6")]
    public void Only_an_administrator_may_approve_on_behalf_of_a_maintainer()
    {
        var (parent, child, link) = PendingLink();

        Assert.Throws<ApprovalNotAdministratorException>(
            () => link.ApproveAsAdministrator(child, Alice, actorIsAdministrator: false, NoAncestors(parent)));

        Assert.Equal(EndpointApproval.Pending, link.ChildApproval);
        Assert.Empty(link.PendingEvents);
    }

    [Fact]
    [Trait("AC", "AC-03.7")]
    [Trait("AC", "AC-03.5")]
    public void An_administrator_cannot_undo_a_maintainers_decline_or_withdrawal()
    {
        var (parent, child, declined) = PendingLink();
        declined.Disagree(child, parent, Bob);
        declined.TakeEvents();

        var (otherParent, otherChild, withdrawn) = PendingLink();
        withdrawn.Approve(otherChild, Bob, NoAncestors(otherParent));
        withdrawn.Disagree(otherChild, otherParent, Bob);
        withdrawn.TakeEvents();

        Assert.Throws<ApprovalNotPendingException>(
            () => declined.ApproveAsAdministrator(child, Admin, actorIsAdministrator: true, NoAncestors(parent)));
        Assert.Throws<ApprovalNotPendingException>(
            () => withdrawn.ApproveAsAdministrator(otherChild, Admin, actorIsAdministrator: true, NoAncestors(otherParent)));

        Assert.False(declined.IsActive);
        Assert.False(withdrawn.IsActive);
        Assert.Empty(declined.PendingEvents);
        Assert.Empty(withdrawn.PendingEvents);
    }

    [Fact]
    [Trait("AC", "AC-03.6")]
    public void Approving_an_approved_side_again_is_rejected()
    {
        var (parent, _, link) = PendingLink();

        Assert.Throws<ConsentUnchangedException>(
            () => link.ApproveAsAdministrator(parent, Admin, actorIsAdministrator: true, NoAncestors(parent)));
    }

    [Fact]
    [Trait("AC", "AC-03.6")]
    public void A_deleted_link_or_profile_accepts_no_administrator_approval()
    {
        var parent = NewPerson("Hari", Alice);
        var child = NewPerson("Maya", Alice);
        var deleted = ParentChildLink.Create(parent, child, ParentChildType.Biological, Alice, NoParents, NoAncestors(parent), married: false);
        deleted.Delete(parent, child, Alice, Now);

        var (otherParent, otherChild, link) = PendingLink();
        otherChild.Delete(Bob, actorIsAdministrator: false, Now);

        Assert.Throws<LinkDeletedException>(
            () => deleted.ApproveAsAdministrator(child, Admin, actorIsAdministrator: true, NoAncestors(parent)));
        Assert.Throws<ProfileDeletedException>(
            () => link.ApproveAsAdministrator(otherChild, Admin, actorIsAdministrator: true, NoAncestors(otherParent)));
    }

    [Fact]
    [Trait("AC", "AC-03.6")]
    public void Activating_through_an_administrator_approval_runs_the_cycle_check()
    {
        var (parent, child, link) = PendingLink();
        var ancestorsWithChild = new AncestorSnapshot(parent.Id, new HashSet<PersonId> { child.Id }, isComplete: true);

        Assert.Throws<LinkCycleDetectedException>(
            () => link.ApproveAsAdministrator(child, Admin, actorIsAdministrator: true, ancestorsWithChild));

        Assert.Equal(EndpointApproval.Pending, link.ChildApproval);
    }

    [Fact]
    [Trait("AC", "AC-03.6")]
    [Trait("AC", "AC-03.8")]
    public void An_administrator_approves_a_pending_spouse_and_the_marriage_becomes_active()
    {
        var (_, sita, marriage) = PendingMarriage();

        marriage.ApproveAsAdministrator(sita, Admin, actorIsAdministrator: true);

        Assert.True(marriage.IsActive);
        Assert.Equal([new MarriageSpouseApprovedByAdministrator(marriage.Id, sita.Id, Admin, Bob)], marriage.PendingEvents);
    }

    [Fact]
    [Trait("AC", "AC-03.6")]
    [Trait("AC", "AC-03.7")]
    public void Marriage_administrator_approval_needs_an_administrator_and_a_pending_side()
    {
        var (ram, sita, marriage) = PendingMarriage();

        Assert.Throws<ApprovalNotAdministratorException>(() => marriage.ApproveAsAdministrator(sita, Alice, actorIsAdministrator: false));
        Assert.Throws<MarriageConsentUnchangedException>(() => marriage.ApproveAsAdministrator(ram, Admin, actorIsAdministrator: true));

        marriage.Disagree(sita, ram, Bob);
        Assert.Throws<ApprovalNotPendingException>(() => marriage.ApproveAsAdministrator(sita, Admin, actorIsAdministrator: true));
        Assert.False(marriage.IsActive);
    }
}
