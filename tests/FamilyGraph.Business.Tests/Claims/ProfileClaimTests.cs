using FamilyGraph.Business.Audit;
using FamilyGraph.Business.Claims;
using FamilyGraph.Business.Handovers;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Business.Tests.Claims;

public class ProfileClaimTests
{
    private static readonly AccountId Alice = new(Guid.NewGuid());
    private static readonly AccountId Bob = new(Guid.NewGuid());
    private static readonly AccountId Carol = new(Guid.NewGuid());
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    private static Person NewPerson(string firstName, AccountId maintainer)
    {
        var person = Person.Create(new PersonName(firstName, null, "Rai"), maintainer);
        person.TakeEvents();
        return person;
    }

    private static AncestorSnapshot NoAncestors(Person person) => new(person.Id, new HashSet<PersonId>(), isComplete: true);

    private static ParentChildLink Link(Person parent, Person child, AccountId creator) =>
        ParentChildLink.Create(parent, child, ParentChildType.Biological, creator, new HashSet<PersonId>(), NoAncestors(parent), married: false);

    private static Marriage Marry(Person spouse, Person other, AccountId creator) =>
        Marriage.Create(spouse, other, creator, alreadyMarried: false, directlyRelated: false);

    private static ProfileClaim Submit(Person person, AccountId claimant, string? evidence = "I am Maya.")
    {
        var claim = ProfileClaim.Submit(person, claimant, evidence, claimantHasClaim: false, Now);
        claim.TakeEvents();
        return claim;
    }

    [Fact]
    [Trait("AC", "AC-04.1")]
    public void A_signed_in_account_submits_an_open_claim_with_a_note()
    {
        var person = NewPerson("Maya", Alice);

        var claim = ProfileClaim.Submit(person, Bob, "  I am Maya, ask my aunt Sita.  ", claimantHasClaim: false, Now);

        Assert.Equal(ClaimState.Open, claim.State);
        Assert.Equal((person.Id, Bob, "I am Maya, ask my aunt Sita.", Now), (claim.PersonId, claim.ClaimantId, claim.Evidence, claim.SubmittedAt));
        Assert.Equal([new ClaimSubmitted(claim.Id, person.Id, Bob, HasEvidence: true)], claim.PendingEvents);
    }

    [Fact]
    [Trait("AC", "AC-04.14")]
    public void The_note_is_optional_and_at_most_2000_characters()
    {
        var person = NewPerson("Maya", Alice);

        Assert.Null(ProfileClaim.Submit(person, Bob, "   ", claimantHasClaim: false, Now).Evidence);
        Assert.Equal(2000, ProfileClaim.Submit(person, Bob, new string('a', 2000), claimantHasClaim: false, Now).Evidence!.Length);
        var error = Assert.Throws<ClaimEvidenceTooLongException>(
            () => ProfileClaim.Submit(person, Bob, new string('a', 2001), claimantHasClaim: false, Now));
        Assert.Equal("claim.evidence_too_long", error.Code);
    }

    [Fact]
    [Trait("AC", "AC-04.15")]
    public void A_deleted_profile_cannot_be_claimed()
    {
        var person = NewPerson("Maya", Alice);
        person.Delete(Alice, actorIsAdministrator: false, Now);

        Assert.Throws<ProfileDeletedException>(() => ProfileClaim.Submit(person, Bob, null, claimantHasClaim: false, Now));
    }

    [Fact]
    [Trait("AC", "AC-04.11")]
    public void The_maintainer_cannot_claim_their_own_profile_by_request()
    {
        var person = NewPerson("Maya", Alice);

        var error = Assert.Throws<ClaimOwnProfileException>(() => ProfileClaim.Submit(person, Alice, null, claimantHasClaim: false, Now));

        Assert.Equal("claim.own_profile", error.Code);
    }

    [Fact]
    [Trait("AC", "AC-04.12")]
    public void An_account_with_a_claimed_profile_or_open_claim_cannot_claim_again()
    {
        var person = NewPerson("Maya", Alice);

        var error = Assert.Throws<AccountAlreadyClaimedException>(() => ProfileClaim.Submit(person, Bob, null, claimantHasClaim: true, Now));

        Assert.Equal("claim.account_has_claim", error.Code);
    }

    [Fact]
    [Trait("AC", "AC-04.3")]
    public void Approval_makes_the_claimant_the_maintainer_and_the_claimed_person()
    {
        var person = NewPerson("Maya", Alice);
        var claim = Submit(person, Bob);
        var later = Now.AddDays(1);

        claim.Approve(person, Alice, openClaimsOnProfile: 1, later);

        Assert.Equal(ClaimState.Approved, claim.State);
        Assert.Equal((Alice, (DateTimeOffset?)later), (claim.ClosedById!.Value, claim.ClosedAt));
        Assert.Equal(Bob, person.MaintainerId);
        Assert.Equal(Bob, person.ClaimedById);
        Assert.True(person.IsClaimed);
        Assert.Equal([new ClaimApproved(claim.Id, person.Id, Alice, Bob)], claim.PendingEvents);
        Assert.Empty(person.PendingEvents);
    }

    [Fact]
    [Trait("AC", "AC-04.1")]
    public void Rejection_changes_nothing_about_the_profile()
    {
        var person = NewPerson("Maya", Alice);
        var claim = Submit(person, Bob);

        claim.Reject(person, Alice, openClaimsOnProfile: 1, Now);

        Assert.Equal(ClaimState.Rejected, claim.State);
        Assert.Equal(Alice, person.MaintainerId);
        Assert.False(person.IsClaimed);
        Assert.Equal([new ClaimRejected(claim.Id, person.Id, Alice, Bob)], claim.PendingEvents);
    }

    [Fact]
    [Trait("AC", "AC-04.1")]
    public void Only_the_maintainer_decides()
    {
        var person = NewPerson("Maya", Alice);
        var claim = Submit(person, Bob);

        Assert.Throws<NotProfileMaintainerException>(() => claim.Approve(person, Carol, 1, Now));
        Assert.Throws<NotProfileMaintainerException>(() => claim.Reject(person, Bob, 1, Now));
        Assert.Equal(ClaimState.Open, claim.State);
    }

    [Fact]
    [Trait("AC", "AC-04.7")]
    public void With_several_open_claims_the_maintainer_cannot_decide()
    {
        var person = NewPerson("Maya", Alice);
        var claim = Submit(person, Bob);

        var error = Assert.Throws<ClaimNeedsAdministratorException>(() => claim.Approve(person, Alice, openClaimsOnProfile: 2, Now));
        Assert.Throws<ClaimNeedsAdministratorException>(() => claim.Reject(person, Alice, openClaimsOnProfile: 2, Now));

        Assert.Equal("claim.administrator_only", error.Code);
        Assert.False(ProfileClaim.MaintainerMayDecide(person, 2));
        Assert.True(ProfileClaim.MaintainerMayDecide(person, 1));
    }

    [Fact]
    [Trait("AC", "AC-04.9")]
    public void A_claim_on_a_claimed_profile_is_not_the_maintainers_to_decide()
    {
        var person = NewPerson("Maya", Alice);
        person.ClaimAsSelf(Alice, actorHasClaim: false);
        var claim = Submit(person, Bob);

        Assert.Throws<ClaimNeedsAdministratorException>(() => claim.Approve(person, Alice, openClaimsOnProfile: 1, Now));
        Assert.Equal(Alice, person.MaintainerId);
    }

    [Fact]
    [Trait("AC", "AC-04.1")]
    public void A_decided_claim_cannot_be_decided_again()
    {
        var person = NewPerson("Maya", Alice);
        var claim = Submit(person, Bob);
        claim.Reject(person, Alice, 1, Now);

        var error = Assert.Throws<ClaimClosedException>(() => claim.Approve(person, Alice, 1, Now));

        Assert.Equal("claim.closed", error.Code);
    }

    [Fact]
    [Trait("AC", "AC-04.13")]
    public void The_claimant_withdraws_an_open_claim()
    {
        var person = NewPerson("Maya", Alice);
        var claim = Submit(person, Bob);

        Assert.Throws<NotClaimantException>(() => claim.Withdraw(Alice, Now));
        claim.Withdraw(Bob, Now);

        Assert.Equal(ClaimState.Withdrawn, claim.State);
        Assert.Equal([new ClaimWithdrawn(claim.Id, person.Id, Bob)], claim.PendingEvents);
        Assert.Throws<ClaimClosedException>(() => claim.Withdraw(Bob, Now));
    }

    [Fact]
    [Trait("AC", "AC-04.15")]
    public void Deleting_the_profile_cancels_its_open_claims()
    {
        var person = NewPerson("Maya", Alice);
        var claim = Submit(person, Bob);
        Assert.Throws<InvalidOperationException>(() => claim.CancelForDeletedProfile(person, Alice, Now));

        person.Delete(Alice, actorIsAdministrator: false, Now);
        claim.CancelForDeletedProfile(person, Alice, Now);

        Assert.Equal(ClaimState.Cancelled, claim.State);
        Assert.Equal([new ClaimCancelled(claim.Id, person.Id, Alice, Bob)], claim.PendingEvents);
    }

    [Fact]
    [Trait("AC", "AC-04.4")]
    public void Approval_leaves_link_and_marriage_consent_unchanged()
    {
        var maya = NewPerson("Maya", Alice);
        var father = NewPerson("Hari", Carol);
        var spouse = NewPerson("Ram", Carol);
        var link = Link(father, maya, Alice);
        var marriage = Marry(maya, spouse, Alice);
        var before = (link.ParentApproval, link.ChildApproval, link.IsActive, marriage.FirstApproval, marriage.SecondApproval);
        var claim = Submit(maya, Bob);

        claim.Approve(maya, Alice, 1, Now);

        Assert.Equal(before, (link.ParentApproval, link.ChildApproval, link.IsActive, marriage.FirstApproval, marriage.SecondApproval));
    }

    [Fact]
    [Trait("AC", "AC-04.4")]
    public void Claim_events_are_identity_claim_audit_records_apart_from_consent()
    {
        var person = NewPerson("Maya", Alice);
        var submitted = ProfileClaim.Submit(person, Bob, null, claimantHasClaim: false, Now);

        Assert.All(submitted.PendingEvents, e => Assert.Equal(AuditFamily.IdentityClaim, e.Family));
        Assert.NotEqual(AuditFamily.RelationshipConsent, submitted.PendingEvents.Single().Family);
    }

    [Fact]
    [Trait("AC", "AC-04.11")]
    public void The_maintainer_marks_a_profile_as_themselves()
    {
        var person = NewPerson("Maya", Alice);

        person.ClaimAsSelf(Alice, actorHasClaim: false);

        Assert.Equal(Alice, person.ClaimedById);
        Assert.Equal([new ProfileClaimedAsSelf(person.Id, Alice)], person.PendingEvents);
        Assert.Equal(AuditFamily.IdentityClaim, person.PendingEvents.Single().Family);
    }

    [Fact]
    [Trait("AC", "AC-04.11")]
    public void Only_the_maintainer_marks_a_profile_as_themselves_and_only_once()
    {
        var person = NewPerson("Maya", Alice);

        Assert.Throws<NotProfileMaintainerException>(() => person.ClaimAsSelf(Bob, actorHasClaim: false));
        person.ClaimAsSelf(Alice, actorHasClaim: false);
        var error = Assert.Throws<ProfileClaimedException>(() => person.ClaimAsSelf(Alice, actorHasClaim: false));
        Assert.Equal("person.claimed", error.Code);
    }

    [Fact]
    [Trait("AC", "AC-04.12")]
    public void An_account_with_a_claim_cannot_mark_another_profile_as_themselves()
    {
        var person = NewPerson("Maya", Alice);

        Assert.Throws<AccountAlreadyClaimedException>(() => person.ClaimAsSelf(Alice, actorHasClaim: true));
        Assert.False(person.IsClaimed);
    }

    [Fact]
    [Trait("AC", "AC-04.11")]
    public void Deleting_a_claimed_profile_frees_the_account()
    {
        var person = NewPerson("Maya", Alice);
        person.ClaimAsSelf(Alice, actorHasClaim: false);

        person.Delete(Alice, actorIsAdministrator: false, Now);

        Assert.Null(person.ClaimedById);
    }

    [Fact]
    [Trait("AC", "AC-01.17")]
    public void A_claimed_profile_cannot_be_handed_over()
    {
        var person = NewPerson("Maya", Alice);
        var handover = ProfileHandover.Offer(person, Alice, "hash", Now, Now.AddDays(14));
        person.ClaimAsSelf(Alice, actorHasClaim: false);

        Assert.Throws<ProfileClaimedException>(() => ProfileHandover.Offer(person, Alice, "hash2", Now, Now.AddDays(14)));
        Assert.Throws<ProfileClaimedException>(() => person.AcceptHandover(handover, Bob, Now));
        Assert.Equal(Alice, person.MaintainerId);
    }

    [Fact]
    [Trait("AC", "AC-01.28")]
    public void A_claimed_profile_cannot_be_transferred()
    {
        var person = NewPerson("Maya", Alice);
        person.ClaimAsSelf(Alice, actorHasClaim: false);

        Assert.Throws<ProfileClaimedException>(() => person.Transfer(Bob, Carol, actorIsAdministrator: true));
        Assert.Equal(Alice, person.MaintainerId);
    }

    [Fact]
    [Trait("AC", "AC-03.10")]
    public void An_administrator_cannot_approve_on_behalf_of_a_claimed_person()
    {
        var maya = NewPerson("Maya", Alice);
        var father = NewPerson("Hari", Carol);
        var spouse = NewPerson("Ram", Carol);
        var link = Link(father, maya, Carol);
        var marriage = Marry(spouse, maya, Carol);
        maya.ClaimAsSelf(Alice, actorHasClaim: false);

        var error = Assert.Throws<ApprovalForClaimedPersonException>(
            () => link.ApproveAsAdministrator(maya, Bob, actorIsAdministrator: true, NoAncestors(father)));
        Assert.Throws<ApprovalForClaimedPersonException>(() => marriage.ApproveAsAdministrator(maya, Bob, actorIsAdministrator: true));

        Assert.Equal("consent.claimed_person", error.Code);
        Assert.Equal(EndpointApproval.Pending, link.ChildApproval);
    }
}
