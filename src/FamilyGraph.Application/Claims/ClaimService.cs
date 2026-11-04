using FamilyGraph.Application.People;
using FamilyGraph.Application.Persistence;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.Claims;
using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Claims;

/// <summary>
/// Identity claims (PRD 04, #25, D-051): a signed-in account asks to be recognised as the person a
/// profile describes, with an optional note, and the maintainer approves or rejects while it is the
/// only open claim on an unclaimed profile. A maintainer may instead mark a profile as themselves.
/// An account has at most one claimed profile or open claim. <see cref="ProfileClaim"/> and
/// <see cref="Person"/> enforce the rules; relationship consent never changes here.
/// </summary>
public sealed class ClaimService(
    IPersonStore people,
    IPersonQueries personQueries,
    IClaimStore claims,
    IClaimQueries queries,
    IAuditLog audit,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    ClaimLimits limits)
{
    public async Task<ClaimView> SubmitAsync(PersonId personId, string? evidence, Viewer viewer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);

        var person = await people.FindAsync(personId, cancellationToken) ?? throw new PersonNotFoundException();
        var hasClaim = await claims.AccountHasClaimAsync(viewer.AccountId, cancellationToken);
        var now = time.GetUtcNow();
        var claim = ProfileClaim.Submit(person, viewer.AccountId, evidence, hasClaim, now);
        claims.Add(claim);
        audit.Add(claim.TakeEvents(), now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return View(ClaimRecord.From(claim, PersonRecord.From(person, viewer.AccountId)), viewer);
    }

    /// <summary>"This is me": the maintainer marks a profile they maintain as themselves (D-051).</summary>
    public async Task ClaimAsSelfAsync(PersonId personId, AccountId actorId, CancellationToken cancellationToken)
    {
        var person = await people.FindAsync(personId, cancellationToken) ?? throw new PersonNotFoundException();
        var hasClaim = await claims.AccountHasClaimAsync(actorId, cancellationToken);
        person.ClaimAsSelf(actorId, hasClaim);
        audit.Add(person.TakeEvents(), time.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task ApproveAsync(ClaimId claimId, AccountId actorId, CancellationToken cancellationToken) =>
        DecideAsync(claimId, actorId, approve: true, cancellationToken);

    public Task RejectAsync(ClaimId claimId, AccountId actorId, CancellationToken cancellationToken) =>
        DecideAsync(claimId, actorId, approve: false, cancellationToken);

    public async Task WithdrawAsync(ClaimId claimId, AccountId actorId, CancellationToken cancellationToken)
    {
        var claim = await claims.FindAsync(claimId, cancellationToken) ?? throw new ClaimNotFoundException();
        var now = time.GetUtcNow();
        claim.Withdraw(actorId, now);
        audit.Add(claim.TakeEvents(), now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The claim state of one profile for the viewer, for the person page.</summary>
    public async Task<ProfileClaimStatus> GetForPersonAsync(PersonId personId, Viewer viewer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);

        var person = await people.FindAsync(personId, cancellationToken) ?? throw new PersonNotFoundException();
        var record = PersonRecord.From(person, viewer.AccountId);
        var open = await claims.ListOpenForPersonAsync(personId, cancellationToken);
        var maintains = person.IsMaintainedBy(viewer.AccountId);
        var mayDecide = maintains && ProfileClaim.MaintainerMayDecide(person, open.Count);
        var yours = open.SingleOrDefault(claim => claim.ClaimantId == viewer.AccountId);

        return new ProfileClaimStatus(
            IsClaimed: maintains || viewer.IsAdministrator ? person.IsClaimed : null,
            ClaimedByYou: person.ClaimedById == viewer.AccountId,
            YouHaveClaim: await claims.AccountHasClaimAsync(viewer.AccountId, cancellationToken),
            YourOpenClaim: yours is null ? null : View(ClaimRecord.From(yours, record), viewer),
            ToDecide: mayDecide ? [.. open.Select(claim => View(ClaimRecord.From(claim, record), viewer))] : [],
            NeedsAdministrator: maintains && open.Count > 0 && !mayDecide);
    }

    /// <summary>The claims waiting for the viewer's decision as maintainer, for the Approvals page.</summary>
    public async Task<ClaimList> ListToDecideAsync(Viewer viewer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);

        var list = await queries.ListToDecideAsync(viewer.AccountId, limits.ListLimit, cancellationToken);
        return new ClaimList([.. list.Claims.Select(claim => View(claim, viewer))], list.IsTruncated);
    }

    /// <summary>The viewer's own claims, for the Approvals page.</summary>
    public async Task<ClaimList> ListMineAsync(Viewer viewer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);

        var list = await queries.ListByClaimantAsync(viewer.AccountId, limits.ListLimit, cancellationToken);
        return new ClaimList([.. list.Claims.Select(claim => View(claim, viewer))], list.IsTruncated);
    }

    private async Task DecideAsync(ClaimId claimId, AccountId actorId, bool approve, CancellationToken cancellationToken)
    {
        var claim = await claims.FindAsync(claimId, cancellationToken) ?? throw new ClaimNotFoundException();
        var person = await people.FindAsync(claim.PersonId, cancellationToken) ?? throw new PersonNotFoundException();
        var open = await claims.ListOpenForPersonAsync(person.Id, cancellationToken);
        var now = time.GetUtcNow();
        if (approve)
        {
            claim.Approve(person, actorId, open.Count, now);
        }
        else
        {
            claim.Reject(person, actorId, open.Count, now);
        }

        audit.Add(claim.TakeEvents(), now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Every view goes to the claimant or the maintainer deciding the claim, who both see the note
    /// (AC-04.5). Other maintainers and accounts never get a claim's view.
    /// </summary>
    private static ClaimView View(ClaimRecord claim, Viewer viewer) =>
        new(claim.Id, PrivacyProjection.Project(claim.Person, viewer), claim.State, claim.Evidence, claim.SubmittedAt, claim.ClosedAt);
}
