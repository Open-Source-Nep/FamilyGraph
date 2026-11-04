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

    public ClaimServiceTests()
    {
        _claims = new InMemoryClaims(_people);
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static Viewer As(AccountId account, bool administrator = false) => new(account, administrator);

    private ClaimService Service() =>
        new(_people, _people, _claims, _claims, _people, _people, new FixedTimeProvider(Now), new ClaimLimits());

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
        Assert.Equal("I am Maya.", claimant.YourOpenClaim!.Evidence);
        Assert.Empty(claimant.ToDecide);
        Assert.Null(other.YourOpenClaim);
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
}
