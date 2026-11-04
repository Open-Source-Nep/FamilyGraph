using FamilyGraph.Application.Claims;
using FamilyGraph.Application.People;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Application.Tests.People;
using FamilyGraph.Business.Audit;
using FamilyGraph.Business.Claims;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Application.Tests.Claims;

public class ClaimServiceTests
{
    private static readonly AccountId Alice = new(Guid.NewGuid());
    private static readonly AccountId Bob = new(Guid.NewGuid());
    private static readonly AccountId Carol = new(Guid.NewGuid());
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    private readonly InMemoryPeople _people = new();
    private readonly InMemoryClaims _claims;
    private readonly FixedTimeProvider _time = new(Now);

    public ClaimServiceTests()
    {
        _claims = new InMemoryClaims(_people);
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static Viewer As(AccountId account, bool administrator = false) => new(account, administrator);

    private ClaimService Service() =>
        new(_people, _claims, _claims, _people, _people, _time, new ClaimLimits());

    private Person AddPerson(string firstName, AccountId maintainer)
    {
        var person = Person.Create(new PersonName(firstName), maintainer);
        person.TakeEvents();
        _people.People[person.Id] = person;
        return person;
    }

    [Fact]
    [Trait("AC", "AC-04.1")]
    public async Task Submit_opens_a_claim_and_records_an_identity_claim_event_without_the_note()
    {
        var person = AddPerson("Maya", Alice);

        var view = await Service().SubmitAsync(person.Id, "I am Maya.", As(Bob), Cancellation);

        var claim = Assert.Single(_claims.All);
        Assert.Equal((claim.Id, ClaimState.Open, "I am Maya."), (view.Id, view.State, view.Evidence));
        var recorded = Assert.Single(_people.AuditEvents);
        Assert.Equal(new ClaimSubmitted(claim.Id, person.Id, Bob, HasEvidence: true), recorded.Event);
        Assert.Equal(AuditFamily.IdentityClaim, recorded.Event.Family);
    }

    [Fact]
    [Trait("AC", "AC-04.12")]
    public async Task An_account_with_an_open_claim_cannot_claim_again_until_it_withdraws()
    {
        var maya = AddPerson("Maya", Alice);
        var sita = AddPerson("Sita", Alice);
        var first = await Service().SubmitAsync(maya.Id, null, As(Bob), Cancellation);

        await Assert.ThrowsAsync<AccountAlreadyClaimedException>(() => Service().SubmitAsync(sita.Id, null, As(Bob), Cancellation));
        await Assert.ThrowsAsync<AccountAlreadyClaimedException>(() => Service().SubmitAsync(maya.Id, null, As(Bob), Cancellation));

        await Service().WithdrawAsync(first.Id, Bob, Cancellation);
        await Service().SubmitAsync(sita.Id, null, As(Bob), Cancellation);
        Assert.Equal(2, _claims.All.Count);
    }

    [Fact]
    [Trait("AC", "AC-04.12")]
    public async Task An_account_with_a_claimed_profile_cannot_claim_or_mark_another()
    {
        var mine = AddPerson("Bob", Bob);
        var other = AddPerson("Ram", Bob);
        var maya = AddPerson("Maya", Alice);
        await Service().ClaimAsSelfAsync(mine.Id, Bob, Cancellation);

        await Assert.ThrowsAsync<AccountAlreadyClaimedException>(() => Service().SubmitAsync(maya.Id, null, As(Bob), Cancellation));
        await Assert.ThrowsAsync<AccountAlreadyClaimedException>(() => Service().ClaimAsSelfAsync(other.Id, Bob, Cancellation));
    }

    [Fact]
    [Trait("AC", "AC-04.11")]
    public async Task ClaimAsSelf_marks_the_profile_and_records_a_self_approved_claim()
    {
        var person = AddPerson("Maya", Alice);

        await Service().ClaimAsSelfAsync(person.Id, Alice, Cancellation);

        Assert.Equal(Alice, person.ClaimedById);
        Assert.Equal(new ProfileClaimedAsSelf(person.Id, Alice), Assert.Single(_people.AuditEvents).Event);
        var status = await Service().GetForPersonAsync(person.Id, As(Alice), Cancellation);
        Assert.True(status.ClaimedByYou);
        Assert.True(status.IsClaimed);
    }

    [Fact]
    [Trait("AC", "AC-04.3")]
    [Trait("AC", "AC-04.4")]
    public async Task Approve_hands_the_profile_to_the_claimant_and_leaves_consent_unchanged()
    {
        var maya = AddPerson("Maya", Alice);
        var father = AddPerson("Hari", Carol);
        var link = ParentChildLink.Create(
            father, maya, ParentChildType.Biological, Alice, new HashSet<PersonId>(), new AncestorSnapshot(father.Id, new HashSet<PersonId>(), isComplete: true), married: false);
        link.TakeEvents();
        _people.Links[link.Id] = link;
        var before = (link.ParentApproval, link.ChildApproval);
        var claim = await Service().SubmitAsync(maya.Id, null, As(Bob), Cancellation);

        await Service().ApproveAsync(claim.Id, Alice, Cancellation);

        Assert.Equal((Bob, (AccountId?)Bob), (maya.MaintainerId, maya.ClaimedById));
        Assert.Equal(before, (link.ParentApproval, link.ChildApproval));
        Assert.Equal(new ClaimApproved(claim.Id, maya.Id, Alice, Bob), _people.AuditEvents[^1].Event);
        Assert.DoesNotContain(_people.AuditEvents, e => e.Event.Family == AuditFamily.RelationshipConsent);
        Assert.Empty((await Service().ListToDecideAsync(As(Alice), Cancellation)).Claims);
    }

    [Fact]
    [Trait("AC", "AC-04.1")]
    public async Task Reject_closes_the_claim_and_keeps_the_maintainer()
    {
        var maya = AddPerson("Maya", Alice);
        var claim = await Service().SubmitAsync(maya.Id, null, As(Bob), Cancellation);

        await Service().RejectAsync(claim.Id, Alice, Cancellation);

        Assert.Equal(Alice, maya.MaintainerId);
        Assert.Equal(ClaimState.Rejected, Assert.Single(_claims.All).State);
        var mine = await Service().ListMineAsync(As(Bob), Cancellation);
        Assert.Equal(ClaimState.Rejected, Assert.Single(mine.Claims).State);
    }

    [Fact]
    [Trait("AC", "AC-04.7")]
    public async Task With_two_open_claims_the_maintainer_sees_neither_to_decide_and_cannot_decide()
    {
        var maya = AddPerson("Maya", Alice);
        var first = await Service().SubmitAsync(maya.Id, "note", As(Bob), Cancellation);
        await Service().SubmitAsync(maya.Id, "other", As(Carol), Cancellation);

        await Assert.ThrowsAsync<ClaimNeedsAdministratorException>(() => Service().ApproveAsync(first.Id, Alice, Cancellation));

        Assert.Empty((await Service().ListToDecideAsync(As(Alice), Cancellation)).Claims);
        var status = await Service().GetForPersonAsync(maya.Id, As(Alice), Cancellation);
        Assert.Empty(status.ToDecide);
        Assert.True(status.NeedsAdministrator);
    }

    [Fact]
    [Trait("AC", "AC-04.5")]
    public async Task Only_the_claimant_and_the_deciding_maintainer_see_a_claim_and_its_note()
    {
        var maya = AddPerson("Maya", Alice);
        await Service().SubmitAsync(maya.Id, "I am Maya.", As(Bob), Cancellation);

        var maintainer = await Service().GetForPersonAsync(maya.Id, As(Alice), Cancellation);
        var claimant = await Service().GetForPersonAsync(maya.Id, As(Bob), Cancellation);
        var other = await Service().GetForPersonAsync(maya.Id, As(Carol), Cancellation);
        var administrator = await Service().GetForPersonAsync(maya.Id, As(Carol, administrator: true), Cancellation);

        Assert.Equal("I am Maya.", Assert.Single(maintainer.ToDecide).Evidence);
        Assert.Equal("I am Maya.", claimant.YourClaim!.Evidence);
        Assert.Empty(claimant.ToDecide);
        Assert.Null(other.YourClaim);
        Assert.Empty(other.ToDecide);
        Assert.Null(other.IsClaimed);
        Assert.Empty(administrator.ToDecide);
        Assert.False(administrator.IsClaimed);
        Assert.Empty((await Service().ListToDecideAsync(As(Carol), Cancellation)).Claims);
        Assert.Empty((await Service().ListMineAsync(As(Alice), Cancellation)).Claims);
    }

    [Fact]
    [Trait("AC", "AC-04.13")]
    public async Task Only_the_claimant_withdraws()
    {
        var maya = AddPerson("Maya", Alice);
        var claim = await Service().SubmitAsync(maya.Id, null, As(Bob), Cancellation);

        await Assert.ThrowsAsync<NotClaimantException>(() => Service().WithdrawAsync(claim.Id, Alice, Cancellation));
        await Service().WithdrawAsync(claim.Id, Bob, Cancellation);

        Assert.Equal(ClaimState.Withdrawn, Assert.Single(_claims.All).State);
    }

    [Fact]
    [Trait("AC", "AC-04.15")]
    public async Task Deleting_a_profile_cancels_its_open_claims()
    {
        var maya = AddPerson("Maya", Alice);
        await Service().SubmitAsync(maya.Id, null, As(Bob), Cancellation);
        var people = new PersonService(_people, _people, _people, _claims, _people, _people, new FixedTimeProvider(Now), new PeopleLimits());

        await people.DeleteAsync(maya.Id, As(Alice), Cancellation);

        var claim = Assert.Single(_claims.All);
        Assert.Equal(ClaimState.Cancelled, claim.State);
        Assert.Contains(_people.AuditEvents, e => e.Event == new ClaimCancelled(claim.Id, maya.Id, Alice, Bob));
        Assert.False((await Service().GetForPersonAsync(maya.Id, As(Bob), Cancellation)).YouHaveClaim);
    }

    [Fact]
    public async Task Unknown_claims_and_people_are_not_found()
    {
        await Assert.ThrowsAsync<ClaimNotFoundException>(() => Service().ApproveAsync(ClaimId.New(), Alice, Cancellation));
        await Assert.ThrowsAsync<PersonNotFoundException>(
            () => Service().SubmitAsync(PersonId.New(), null, As(Bob), Cancellation));
    }

    [Fact]
    [Trait("AC", "AC-04.2")]
    [Trait("AC", "AC-04.8")]
    public async Task A_rejected_claimant_appeals_and_an_administrator_decides_with_a_rationale()
    {
        var maya = AddPerson("Maya", Alice);
        var claim = await Service().SubmitAsync(maya.Id, "I am Maya.", As(Bob), Cancellation);
        await Service().RejectAsync(claim.Id, Alice, Cancellation);

        var rejected = (await Service().GetForPersonAsync(maya.Id, As(Bob), Cancellation)).YourClaim!;
        Assert.True(rejected.MayRequestReview);
        await Service().RequestReviewAsync(claim.Id, Bob, Cancellation);

        Assert.Empty((await Service().ListToDecideAsync(As(Alice), Cancellation)).Claims);
        Assert.True((await Service().GetForPersonAsync(maya.Id, As(Alice), Cancellation)).NeedsAdministrator);
        await Assert.ThrowsAsync<ClaimNotAdministratorException>(() => Service().ListForAdministratorsAsync(As(Carol), Cancellation));
        var review = Assert.Single((await Service().ListForAdministratorsAsync(As(Carol, administrator: true), Cancellation)).Claims);
        Assert.Equal((claim.Id, "I am Maya.", true), (review.Id, review.Evidence, review.Appealed));

        await Service().ApproveAsAdministratorAsync(claim.Id, "Her aunt confirmed it.", As(Carol, administrator: true), Cancellation);

        Assert.Equal((Bob, (AccountId?)Bob), (maya.MaintainerId, maya.ClaimedById));
        Assert.Equal(new ClaimApprovedByAdministrator(claim.Id, maya.Id, Carol, Bob, null), _people.AuditEvents[^1].Event);
        var mine = Assert.Single((await Service().ListMineAsync(As(Bob), Cancellation)).Claims);
        Assert.Null(mine.Rationale);
        Assert.True(mine.DecidedByAdministrator);
    }

    [Fact]
    [Trait("AC", "AC-04.6")]
    public async Task An_unanswered_claim_can_go_to_review_after_the_response_time()
    {
        var maya = AddPerson("Maya", Alice);
        var claim = await Service().SubmitAsync(maya.Id, null, As(Bob), Cancellation);
        Assert.Equal(Now.AddDays(30), claim.ReviewAvailableAt);

        await Assert.ThrowsAsync<ClaimReviewTooEarlyException>(() => Service().RequestReviewAsync(claim.Id, Bob, Cancellation));
        _time.Now = Now.AddDays(30);
        Assert.True((await Service().GetForPersonAsync(maya.Id, As(Bob), Cancellation)).YourClaim!.MayRequestReview);
        await Service().RequestReviewAsync(claim.Id, Bob, Cancellation);

        var status = (await Service().GetForPersonAsync(maya.Id, As(Bob), Cancellation)).YourClaim!;
        Assert.True(status.WithAdministrators);
        Assert.False(status.MayRequestReview);
    }

    [Fact]
    [Trait("AC", "AC-04.8")]
    public async Task After_an_administrator_rejection_the_claimant_waits_before_claiming_again()
    {
        var maya = AddPerson("Maya", Alice);
        await Service().SubmitAsync(maya.Id, null, As(Bob), Cancellation);
        var second = await Service().SubmitAsync(maya.Id, null, As(Carol), Cancellation);
        await Service().RejectAsAdministratorAsync(second.Id, "No one confirmed it.", As(Alice, administrator: true), Cancellation);

        await Assert.ThrowsAsync<ClaimWaitingPeriodException>(() => Service().SubmitAsync(maya.Id, null, As(Carol), Cancellation));
        Assert.Equal(Now.AddDays(90), (await Service().GetForPersonAsync(maya.Id, As(Carol), Cancellation)).ClaimAgainFrom);

        _time.Now = Now.AddDays(90);
        await Service().SubmitAsync(maya.Id, "New evidence.", As(Carol), Cancellation);
    }

    [Fact]
    [Trait("AC", "AC-04.5")]
    [Trait("AC", "AC-04.9")]
    public async Task The_claimed_persons_reply_and_the_administrators_question_reach_only_their_readers()
    {
        var maya = AddPerson("Maya", Alice);
        await Service().ClaimAsSelfAsync(maya.Id, Alice, Cancellation);
        var claim = await Service().SubmitAsync(maya.Id, "I am Maya.", As(Bob), Cancellation);
        var admin = As(Carol, administrator: true);

        var toRespond = Assert.Single((await Service().ListToRespondAsync(As(Alice), Cancellation)).Claims);
        Assert.Null(toRespond.Evidence);
        Assert.Single((await Service().GetForPersonAsync(maya.Id, As(Alice), Cancellation)).ToRespond);
        await Service().RespondAsync(claim.Id, "That is my cousin.", Alice, Cancellation);
        await Service().AskForEvidenceAsync(claim.Id, "Who can confirm it?", admin, Cancellation);

        var claimant = (await Service().GetForPersonAsync(maya.Id, As(Bob), Cancellation)).YourClaim!;
        Assert.Equal(("Who can confirm it?", (string?)null), (claimant.AdministratorQuestion, claimant.MaintainerResponse));
        var reviewed = Assert.Single((await Service().ListForAdministratorsAsync(admin, Cancellation)).Claims);
        Assert.Equal(("I am Maya.", "That is my cousin.", (bool?)true), (reviewed.Evidence, reviewed.MaintainerResponse, reviewed.ProfileClaimed));

        await Service().ChangeEvidenceAsync(claim.Id, "My aunt Sita can confirm it.", Bob, Cancellation);
        Assert.Equal("My aunt Sita can confirm it.", Assert.Single(_claims.All).Evidence);
    }

    [Fact]
    [Trait("AC", "AC-04.10")]
    public async Task Notes_are_erased_after_the_retention_period_but_the_rationale_stays()
    {
        var maya = AddPerson("Maya", Alice);
        var open = AddPerson("Sita", Alice);
        var claim = await Service().SubmitAsync(maya.Id, "I am Maya.", As(Bob), Cancellation);
        await Service().SubmitAsync(maya.Id, "Me too.", As(Carol), Cancellation);
        await Service().RejectAsAdministratorAsync(claim.Id, "Not confirmed.", As(Alice, administrator: true), Cancellation);
        await Service().SubmitAsync(open.Id, "Still open.", As(Bob), Cancellation);

        _time.Now = Now.AddDays(89);
        Assert.Equal(0, await Service().EraseExpiredNotesAsync(Cancellation));
        _time.Now = Now.AddDays(91);
        Assert.Equal(1, await Service().EraseExpiredNotesAsync(Cancellation));

        var erased = _claims.All.Single(c => c.Id == claim.Id);
        Assert.Equal((null, "Not confirmed."), (erased.Evidence, erased.Rationale));
        Assert.All(_claims.All.Where(c => c.IsOpen), c => Assert.NotNull(c.Evidence));
    }
}
