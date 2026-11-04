using FamilyGraph.Application.People;
using FamilyGraph.Application.Persistence;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.Claims;
using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Claims;

/// <summary>
/// Identity claims (PRD 04, #25, #26, D-051, D-052): a signed-in account asks to be recognised as
/// the person a profile describes, with an optional note. The maintainer approves or rejects while
/// it is the only open claim on an unclaimed profile; otherwise, or when the claimant asks for review
/// (an appeal, or no answer in time), an administrator decides with a rationale. A maintainer may
/// instead mark a profile as themselves. An account has at most one claimed profile or open claim.
/// <see cref="ProfileClaim"/> and <see cref="Person"/> enforce the rules; relationship consent never
/// changes here.
/// </summary>
public sealed class ClaimService(
    IPersonStore people,
    IClaimStore claims,
    IClaimQueries queries,
    IAuditLog audit,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    ClaimLimits limits)
{
    private const int EraseBatchSize = 500;

    private enum Audience
    {
        Claimant,
        DecidingMaintainer,
        RespondingMaintainer,
        Administrator,
    }

    public async Task<ClaimView> SubmitAsync(PersonId personId, string? evidence, Viewer viewer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);

        var person = await people.FindAsync(personId, cancellationToken) ?? throw new PersonNotFoundException();
        var hasClaim = await claims.AccountHasClaimAsync(viewer.AccountId, cancellationToken);
        var latest = await claims.FindLatestAsync(personId, viewer.AccountId, cancellationToken);
        var now = time.GetUtcNow();
        var claim = ProfileClaim.Submit(person, viewer.AccountId, evidence, hasClaim, now, ClaimAgainFrom(latest));
        claims.Add(claim);
        audit.Add(claim.TakeEvents(), now);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var open = await claims.ListOpenForPersonAsync(personId, cancellationToken);
        return View(ClaimRecord.From(claim, person, viewer.AccountId, open.Count), Audience.Claimant, viewer, claimantHasOtherClaim: false);
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
        ChangeAsync(claimId, (claim, person, open, now) => claim.Approve(person, actorId, open.Count, now), cancellationToken);

    public Task RejectAsync(ClaimId claimId, AccountId actorId, CancellationToken cancellationToken) =>
        ChangeAsync(claimId, (claim, person, open, now) => claim.Reject(person, actorId, open.Count, now), cancellationToken);

    public async Task WithdrawAsync(ClaimId claimId, AccountId actorId, CancellationToken cancellationToken)
    {
        var claim = await claims.FindAsync(claimId, cancellationToken) ?? throw new ClaimNotFoundException();
        var now = time.GetUtcNow();
        claim.Withdraw(actorId, now);
        audit.Add(claim.TakeEvents(), now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The claimant asks administrators to review the claim: an appeal of the maintainer's rejection,
    /// or an open claim the maintainer has not answered in <c>Claims:MaintainerResponseDays</c> (D-052).
    /// </summary>
    public async Task RequestReviewAsync(ClaimId claimId, AccountId actorId, CancellationToken cancellationToken)
    {
        var claim = await claims.FindAsync(claimId, cancellationToken) ?? throw new ClaimNotFoundException();
        var person = await people.FindAsync(claim.PersonId, cancellationToken) ?? throw new PersonNotFoundException();
        var open = await claims.ListOpenForPersonAsync(person.Id, cancellationToken);
        var hasOtherClaim = !claim.IsOpen && await claims.AccountHasClaimAsync(actorId, cancellationToken);
        var now = time.GetUtcNow();
        claim.RequestReview(person, actorId, open.Count, MaintainerUnavailable(claim, now), hasOtherClaim, now);
        audit.Add(claim.TakeEvents(), now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The claimant changes the note of their open claim, for example to answer an administrator.</summary>
    public async Task ChangeEvidenceAsync(ClaimId claimId, string? evidence, AccountId actorId, CancellationToken cancellationToken)
    {
        var claim = await claims.FindAsync(claimId, cancellationToken) ?? throw new ClaimNotFoundException();
        claim.ChangeEvidence(actorId, evidence);
        audit.Add(claim.TakeEvents(), time.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The maintainer of a claimed profile replies to a claim on it, for administrators (AC-04.9).</summary>
    public Task RespondAsync(ClaimId claimId, string? response, AccountId actorId, CancellationToken cancellationToken) =>
        ChangeAsync(claimId, (claim, person, _, _) => claim.Respond(person, actorId, response), cancellationToken);

    /// <summary>An administrator approves a claim waiting for administrators; the profile's other open claims are rejected.</summary>
    public Task ApproveAsAdministratorAsync(ClaimId claimId, string? rationale, Viewer viewer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        return ChangeAsync(
            claimId,
            (claim, person, open, now) => claim.ApproveAsAdministrator(
                person, viewer.AccountId, viewer.IsAdministrator, rationale, [.. open.Where(other => other.Id != claim.Id)], now),
            cancellationToken);
    }

    /// <summary>An administrator rejects a claim waiting for administrators.</summary>
    public Task RejectAsAdministratorAsync(ClaimId claimId, string? rationale, Viewer viewer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        return ChangeAsync(
            claimId,
            (claim, person, open, now) => claim.RejectAsAdministrator(person, viewer.AccountId, viewer.IsAdministrator, rationale, open.Count, now),
            cancellationToken);
    }

    /// <summary>An administrator asks the claimant for more evidence.</summary>
    public Task AskForEvidenceAsync(ClaimId claimId, string? question, Viewer viewer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        return ChangeAsync(
            claimId,
            (claim, person, open, _) => claim.AskForEvidence(person, viewer.AccountId, viewer.IsAdministrator, question, open.Count),
            cancellationToken);
    }

    /// <summary>
    /// Erases the notes of claims closed more than <c>Claims:EvidenceRetentionDays</c> ago, in
    /// batches; the decision and rationale stay (AC-04.10). Returns how many claims it erased.
    /// </summary>
    public async Task<int> EraseExpiredNotesAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var cutoff = now - limits.EvidenceRetention;
        var erased = 0;
        while (true)
        {
            var batch = await claims.ListNotesToEraseAsync(cutoff, EraseBatchSize, cancellationToken);
            foreach (var claim in batch)
            {
                claim.EraseNotes(now);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            erased += batch.Count;
            if (batch.Count < EraseBatchSize)
            {
                return erased;
            }
        }
    }

    /// <summary>The claim state of one profile for the viewer, for the person page.</summary>
    public async Task<ProfileClaimStatus> GetForPersonAsync(PersonId personId, Viewer viewer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);

        var person = await people.FindAsync(personId, cancellationToken) ?? throw new PersonNotFoundException();
        var open = await claims.ListOpenForPersonAsync(personId, cancellationToken);
        var maintains = person.IsMaintainedBy(viewer.AccountId);
        var hasClaim = await claims.AccountHasClaimAsync(viewer.AccountId, cancellationToken);
        var yours = await claims.FindLatestAsync(personId, viewer.AccountId, cancellationToken);
        ClaimRecord Record(ProfileClaim claim) => ClaimRecord.From(claim, person, viewer.AccountId, open.Count);

        return new ProfileClaimStatus(
            IsClaimed: maintains || viewer.IsAdministrator ? person.IsClaimed : null,
            ClaimedByYou: person.ClaimedById == viewer.AccountId,
            YouHaveClaim: hasClaim,
            YourClaim: yours is null ? null : View(Record(yours), Audience.Claimant, viewer, claimantHasOtherClaim: hasClaim && !yours.IsOpen),
            ClaimAgainFrom: ClaimAgainFrom(yours) is { } from && from > time.GetUtcNow() ? from : null,
            ToDecide: maintains ? [.. open.Where(claim => claim.IsForMaintainer(person, open.Count)).Select(claim => View(Record(claim), Audience.DecidingMaintainer, viewer))] : [],
            ToRespond: maintains && person.IsClaimed ? [.. open.Select(claim => View(Record(claim), Audience.RespondingMaintainer, viewer))] : [],
            NeedsAdministrator: maintains && open.Any(claim => claim.IsForAdministrators(person.IsClaimed, open.Count)));
    }

    /// <summary>The claims waiting for the viewer's decision as maintainer, for the Approvals page.</summary>
    public async Task<ClaimList> ListToDecideAsync(Viewer viewer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);

        var list = await queries.ListToDecideAsync(viewer.AccountId, limits.ListLimit, cancellationToken);
        return ToList(list, viewer, Audience.DecidingMaintainer);
    }

    /// <summary>The claims on claimed profiles the viewer maintains, for a reply (AC-04.9), for the Approvals page.</summary>
    public async Task<ClaimList> ListToRespondAsync(Viewer viewer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);

        var list = await queries.ListToRespondAsync(viewer.AccountId, limits.ListLimit, cancellationToken);
        return ToList(list, viewer, Audience.RespondingMaintainer);
    }

    /// <summary>The viewer's own claims, for the Approvals page.</summary>
    public async Task<ClaimList> ListMineAsync(Viewer viewer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);

        var list = await queries.ListByClaimantAsync(viewer.AccountId, limits.ListLimit, cancellationToken);
        var hasClaim = await claims.AccountHasClaimAsync(viewer.AccountId, cancellationToken);
        return new ClaimList(
            [.. list.Claims.Select(claim => View(claim, Audience.Claimant, viewer, claimantHasOtherClaim: hasClaim && !claim.Claim.IsOpen))],
            list.IsTruncated);
    }

    /// <summary>The claims waiting for administrators, with every note, for the administrators' Claims page (D-052).</summary>
    public async Task<ClaimList> ListForAdministratorsAsync(Viewer viewer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        if (!viewer.IsAdministrator)
        {
            throw new ClaimNotAdministratorException();
        }

        var list = await queries.ListForAdministratorsAsync(viewer.AccountId, limits.ListLimit, cancellationToken);
        return ToList(list, viewer, Audience.Administrator);
    }

    private async Task ChangeAsync(
        ClaimId claimId, Action<ProfileClaim, Person, IReadOnlyList<ProfileClaim>, DateTimeOffset> change, CancellationToken cancellationToken)
    {
        var claim = await claims.FindAsync(claimId, cancellationToken) ?? throw new ClaimNotFoundException();
        var person = await people.FindAsync(claim.PersonId, cancellationToken) ?? throw new PersonNotFoundException();
        var open = await claims.ListOpenForPersonAsync(person.Id, cancellationToken);
        var now = time.GetUtcNow();
        change(claim, person, open, now);

        foreach (var other in open.Where(other => other.Id != claim.Id))
        {
            audit.Add(other.TakeEvents(), now);
        }

        audit.Add(claim.TakeEvents(), now);
        audit.Add(person.TakeEvents(), now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private bool MaintainerUnavailable(ProfileClaim claim, DateTimeOffset now) =>
        now - claim.SubmittedAt >= limits.MaintainerResponseTime;

    /// <summary>After an administrator rejected the account's claim on the profile, when it may claim it again (AC-04.8).</summary>
    private DateTimeOffset? ClaimAgainFrom(ProfileClaim? latest) =>
        latest is { State: var state, DecidedByAdministrator: true, ClosedAt: { } closedAt } && state == ClaimState.Rejected
            ? closedAt + limits.NewClaimWait
            : null;

    private ClaimList ToList(ClaimRecords list, Viewer viewer, Audience audience) =>
        new([.. list.Claims.Select(claim => View(claim, audience, viewer))], list.IsTruncated);

    /// <summary>
    /// Each audience sees only its part of a claim (AC-04.5). A view goes only to the claimant, the
    /// maintainer deciding or replying to it, or administrators; other accounts never get one.
    /// </summary>
    private ClaimView View(ClaimRecord record, Audience audience, Viewer viewer, bool claimantHasOtherClaim = false)
    {
        var claim = record.Claim;
        var administrator = audience == Audience.Administrator;
        var claimant = audience == Audience.Claimant;
        var withAdministrators = claim.IsForAdministrators(record.ProfileClaimed, record.OpenClaimsOnProfile);
        var reviewAvailableAt = claimant && claim.IsForMaintainer(record.ProfileClaimed, record.OpenClaimsOnProfile)
            ? claim.SubmittedAt + limits.MaintainerResponseTime
            : (DateTimeOffset?)null;

        return new ClaimView(
            claim.Id,
            PrivacyProjection.Project(record.Person, viewer),
            claim.State,
            audience == Audience.RespondingMaintainer ? null : claim.Evidence,
            claim.SubmittedAt,
            claim.ClosedAt,
            withAdministrators,
            claim.Appealed,
            claim.DecidedByAdministrator,
            claimant || administrator ? claim.AdministratorQuestion : null,
            audience is Audience.RespondingMaintainer or Audience.Administrator ? claim.MaintainerResponse : null,
            administrator ? claim.Rationale : null,
            claimant && claim.MayRequestReview(
                record.Person.IsDeleted, record.ProfileClaimed, record.OpenClaimsOnProfile, MaintainerUnavailable(claim, time.GetUtcNow()), claimantHasOtherClaim),
            reviewAvailableAt,
            administrator ? record.ProfileClaimed : null,
            administrator ? record.OpenClaimsOnProfile : null);
    }
}
