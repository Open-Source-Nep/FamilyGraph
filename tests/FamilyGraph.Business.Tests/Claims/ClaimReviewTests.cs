using FamilyGraph.Business.Claims;
using FamilyGraph.Business.People;

namespace FamilyGraph.Business.Tests.Claims;

/// <summary>Administrator review and appeals of identity claims (#26, D-052).</summary>
public class ClaimReviewTests
{
    private static readonly AccountId Alice = new(Guid.NewGuid());
    private static readonly AccountId Bob = new(Guid.NewGuid());
    private static readonly AccountId Carol = new(Guid.NewGuid());
    private static readonly AccountId Admin = new(Guid.NewGuid());
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    private static Person NewPerson(string firstName, AccountId maintainer)
    {
        var person = Person.Create(new PersonName(firstName), maintainer);
        person.TakeEvents();
        return person;
    }

    private static ProfileClaim Submit(Person person, AccountId claimant, string? evidence = "I am Maya.")
    {
        var claim = ProfileClaim.Submit(person, claimant, evidence, claimantHasClaim: false, Now);
        claim.TakeEvents();
        return claim;
    }

    [Fact]
    [Trait("AC", "AC-04.2")]
    [Trait("AC", "AC-04.8")]
    public void A_rejected_claim_is_appealed_once_and_goes_to_administrators()
    {
        var maya = NewPerson("Maya", Alice);
        var claim = Submit(maya, Bob);
        claim.Reject(maya, Alice, 1, Now);
        claim.TakeEvents();

        Assert.True(claim.MayRequestReview(false, false, 0, maintainerUnavailable: false, claimantHasOtherClaim: false));
        claim.RequestReview(maya, Bob, 0, maintainerUnavailable: false, claimantHasOtherClaim: false, Now.AddDays(1));

        Assert.Equal(ClaimState.Open, claim.State);
        Assert.True(claim.Appealed);
        Assert.Null(claim.ClosedAt);
        Assert.True(claim.IsForAdministrators(profileClaimed: false, openClaimsOnProfile: 1));
        Assert.Throws<ClaimNeedsAdministratorException>(() => claim.Approve(maya, Alice, 1, Now));
        Assert.Equal(new ClaimReviewRequested(claim.Id, maya.Id, Bob, IsAppeal: true), Assert.Single(claim.TakeEvents()));

        claim.RejectAsAdministrator(maya, Admin, actorIsAdministrator: true, "No one could confirm it.", 1, Now.AddDays(2));
        Assert.Throws<ClaimNotAppealableException>(
            () => claim.RequestReview(maya, Bob, 0, maintainerUnavailable: false, claimantHasOtherClaim: false, Now.AddDays(3)));
    }

    [Fact]
    [Trait("AC", "AC-04.6")]
    public void An_unanswered_claim_goes_to_review_only_after_the_response_time()
    {
        var maya = NewPerson("Maya", Alice);
        var claim = Submit(maya, Bob);

        Assert.Throws<ClaimReviewTooEarlyException>(
            () => claim.RequestReview(maya, Bob, 1, maintainerUnavailable: false, claimantHasOtherClaim: false, Now));
        Assert.Throws<NotClaimantException>(
            () => claim.RequestReview(maya, Carol, 1, maintainerUnavailable: true, claimantHasOtherClaim: false, Now));

        claim.RequestReview(maya, Bob, 1, maintainerUnavailable: true, claimantHasOtherClaim: false, Now.AddDays(30));

        Assert.Equal(Now.AddDays(30), claim.ReviewRequestedAt);
        Assert.False(claim.Appealed);
        Assert.False(claim.IsForMaintainer(maya, 1));
        Assert.Throws<ClaimAlreadyWithAdministratorsException>(
            () => claim.RequestReview(maya, Bob, 1, maintainerUnavailable: true, claimantHasOtherClaim: false, Now.AddDays(31)));
    }

    [Fact]
    [Trait("AC", "AC-04.8")]
    public void An_appeal_is_refused_after_the_notes_are_erased_or_while_the_account_has_another_claim()
    {
        var maya = NewPerson("Maya", Alice);
        var claim = Submit(maya, Bob);
        claim.Reject(maya, Alice, 1, Now);

        Assert.Throws<AccountAlreadyClaimedException>(
            () => claim.RequestReview(maya, Bob, 0, maintainerUnavailable: false, claimantHasOtherClaim: true, Now));
        claim.EraseNotes(Now.AddDays(90));
        Assert.Throws<ClaimAppealTooLateException>(
            () => claim.RequestReview(maya, Bob, 0, maintainerUnavailable: false, claimantHasOtherClaim: false, Now.AddDays(91)));
    }

    [Fact]
    [Trait("AC", "AC-04.3")]
    [Trait("AC", "AC-04.7")]
    public void An_administrator_approves_one_of_competing_claims_and_rejects_the_others_with_the_rationale()
    {
        var maya = NewPerson("Maya", Alice);
        var bob = Submit(maya, Bob);
        var carol = Submit(maya, Carol, "I am the real Maya.");

        Assert.Throws<ClaimNotAdministratorException>(
            () => bob.ApproveAsAdministrator(maya, Alice, actorIsAdministrator: false, "Because.", [carol], Now));
        Assert.Throws<ClaimTextRequiredException>(
            () => bob.ApproveAsAdministrator(maya, Admin, actorIsAdministrator: true, "  ", [carol], Now));

        bob.ApproveAsAdministrator(maya, Admin, actorIsAdministrator: true, "Her aunt Sita confirmed it.", [carol], Now);

        Assert.Equal((Bob, (AccountId?)Bob), (maya.MaintainerId, maya.ClaimedById));
        Assert.Equal((ClaimState.Approved, true, "Her aunt Sita confirmed it."), (bob.State, bob.DecidedByAdministrator, bob.Rationale));
        Assert.Equal((ClaimState.Rejected, true, "Her aunt Sita confirmed it."), (carol.State, carol.DecidedByAdministrator, carol.Rationale));
        Assert.Equal(new ClaimApprovedByAdministrator(bob.Id, maya.Id, Admin, Bob, null), Assert.Single(bob.TakeEvents()));
        Assert.Equal(new ClaimRejectedByAdministrator(carol.Id, maya.Id, Admin, Carol), Assert.Single(carol.TakeEvents()));
    }

    [Fact]
    [Trait("AC", "AC-04.9")]
    public void A_claim_on_a_claimed_profile_is_answered_by_its_maintainer_and_decided_by_an_administrator()
    {
        var maya = NewPerson("Maya", Alice);
        maya.ClaimAsSelf(Alice, actorHasClaim: false);
        var claim = Submit(maya, Bob);

        Assert.Throws<ClaimNeedsAdministratorException>(() => claim.Approve(maya, Alice, 1, Now));
        claim.Respond(maya, Alice, "  I am Maya; this is my cousin.  ");
        Assert.Equal("I am Maya; this is my cousin.", claim.MaintainerResponse);
        Assert.Throws<NotProfileMaintainerException>(() => claim.Respond(maya, Carol, "No."));

        claim.ApproveAsAdministrator(maya, Admin, actorIsAdministrator: true, "Documents checked in person.", [], Now);

        Assert.Equal((Bob, (AccountId?)Bob), (maya.MaintainerId, maya.ClaimedById));
        Assert.Contains(new ClaimApprovedByAdministrator(claim.Id, maya.Id, Admin, Bob, Alice), claim.TakeEvents());
    }

    [Fact]
    public void Only_the_maintainer_of_a_claimed_profile_replies()
    {
        var maya = NewPerson("Maya", Alice);
        var claim = Submit(maya, Bob);

        Assert.Throws<ClaimResponseNotNeededException>(() => claim.Respond(maya, Alice, "Hello"));
    }

    [Fact]
    public void An_administrator_does_not_take_a_claim_the_maintainer_still_decides()
    {
        var maya = NewPerson("Maya", Alice);
        var claim = Submit(maya, Bob);

        Assert.Throws<ClaimForMaintainerException>(
            () => claim.RejectAsAdministrator(maya, Admin, actorIsAdministrator: true, "Too early.", 1, Now));
        Assert.Throws<ClaimForMaintainerException>(
            () => claim.AskForEvidence(maya, Admin, actorIsAdministrator: true, "Who are you?", 1));
    }

    [Fact]
    public void An_administrator_asks_for_evidence_and_the_claimant_changes_the_note()
    {
        var maya = NewPerson("Maya", Alice);
        var claim = Submit(maya, Bob);
        claim.RequestReview(maya, Bob, 1, maintainerUnavailable: true, claimantHasOtherClaim: false, Now);
        claim.TakeEvents();

        claim.AskForEvidence(maya, Admin, actorIsAdministrator: true, "Who can confirm it?", 1);
        Assert.Throws<NotClaimantException>(() => claim.ChangeEvidence(Carol, "x"));
        claim.ChangeEvidence(Bob, "My aunt Sita.");

        Assert.Equal(("Who can confirm it?", "My aunt Sita."), (claim.AdministratorQuestion, claim.Evidence));
        Assert.Equal(
            [new ClaimEvidenceRequested(claim.Id, maya.Id, Admin), new ClaimEvidenceChanged(claim.Id, maya.Id, Bob, HasEvidence: true)],
            claim.TakeEvents());
    }

    [Fact]
    [Trait("AC", "AC-04.8")]
    public void A_new_claim_waits_for_the_waiting_period_after_an_administrator_rejection()
    {
        var maya = NewPerson("Maya", Alice);

        var error = Assert.Throws<ClaimWaitingPeriodException>(
            () => ProfileClaim.Submit(maya, Bob, null, claimantHasClaim: false, Now, waitUntil: Now.AddDays(1)));
        Assert.Equal("claim.waiting_period", error.Code);

        Assert.NotNull(ProfileClaim.Submit(maya, Bob, null, claimantHasClaim: false, Now, waitUntil: Now));
    }

    [Fact]
    [Trait("AC", "AC-04.10")]
    public void Erasing_notes_keeps_the_decision_and_rationale()
    {
        var maya = NewPerson("Maya", Alice);
        maya.ClaimAsSelf(Alice, actorHasClaim: false);
        var claim = Submit(maya, Bob);
        claim.Respond(maya, Alice, "Not my cousin.");
        claim.AskForEvidence(maya, Admin, actorIsAdministrator: true, "Who are you?", 1);
        claim.RejectAsAdministrator(maya, Admin, actorIsAdministrator: true, "No one confirmed it.", 1, Now);

        claim.EraseNotes(Now.AddDays(90));

        Assert.Equal((null, null, null), (claim.Evidence, claim.AdministratorQuestion, claim.MaintainerResponse));
        Assert.Equal((ClaimState.Rejected, "No one confirmed it.", (DateTimeOffset?)Now.AddDays(90)), (claim.State, claim.Rationale, claim.NotesErasedAt));
        Assert.Throws<InvalidOperationException>(() => Submit(NewPerson("Sita", Alice), Bob).EraseNotes(Now));
    }
}
