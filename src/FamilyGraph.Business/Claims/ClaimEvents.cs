using FamilyGraph.Business.Audit;
using FamilyGraph.Business.People;

namespace FamilyGraph.Business.Claims;

/// <summary>
/// Identity-claim events (PRD 04, PRD 07). They are their own audit family, apart from relationship
/// consent, and are about the claimed person. None holds the evidence note (D-015).
/// </summary>
public abstract record ClaimEvent(PersonId PersonId, AccountId ActorId) : AuditEvent(ActorId)
{
    public override AuditFamily Family => AuditFamily.IdentityClaim;
}

public sealed record ClaimSubmitted(ClaimId ClaimId, PersonId PersonId, AccountId ActorId, bool HasEvidence)
    : ClaimEvent(PersonId, ActorId);

/// <summary>The maintainer approved; the claimant became the maintainer.</summary>
public sealed record ClaimApproved(ClaimId ClaimId, PersonId PersonId, AccountId ActorId, AccountId ClaimantId)
    : ClaimEvent(PersonId, ActorId);

public sealed record ClaimRejected(ClaimId ClaimId, PersonId PersonId, AccountId ActorId, AccountId ClaimantId)
    : ClaimEvent(PersonId, ActorId);

public sealed record ClaimWithdrawn(ClaimId ClaimId, PersonId PersonId, AccountId ActorId)
    : ClaimEvent(PersonId, ActorId);

/// <summary>The profile was deleted while the claim was open.</summary>
public sealed record ClaimCancelled(ClaimId ClaimId, PersonId PersonId, AccountId ActorId, AccountId ClaimantId)
    : ClaimEvent(PersonId, ActorId);

/// <summary>
/// The maintainer said the profile is themselves ("This is me"): a self-approved identity claim
/// with no request and no evidence (D-051).
/// </summary>
public sealed record ProfileClaimedAsSelf(PersonId PersonId, AccountId ActorId) : ClaimEvent(PersonId, ActorId);

/// <summary>
/// The claimant asked administrators to review the claim (D-052): an appeal of the maintainer's
/// rejection, or an open claim the maintainer did not answer in time.
/// </summary>
public sealed record ClaimReviewRequested(ClaimId ClaimId, PersonId PersonId, AccountId ActorId, bool IsAppeal)
    : ClaimEvent(PersonId, ActorId);

/// <summary>
/// An administrator approved. <see cref="PreviousClaimedById"/> is the account that had claimed the
/// profile before, if any. The rationale stays on the claim, not here (D-015).
/// </summary>
public sealed record ClaimApprovedByAdministrator(
    ClaimId ClaimId, PersonId PersonId, AccountId ActorId, AccountId ClaimantId, AccountId? PreviousClaimedById)
    : ClaimEvent(PersonId, ActorId);

public sealed record ClaimRejectedByAdministrator(ClaimId ClaimId, PersonId PersonId, AccountId ActorId, AccountId ClaimantId)
    : ClaimEvent(PersonId, ActorId);

/// <summary>An administrator asked the claimant for more evidence.</summary>
public sealed record ClaimEvidenceRequested(ClaimId ClaimId, PersonId PersonId, AccountId ActorId)
    : ClaimEvent(PersonId, ActorId);

/// <summary>The claimant changed their note.</summary>
public sealed record ClaimEvidenceChanged(ClaimId ClaimId, PersonId PersonId, AccountId ActorId, bool HasEvidence)
    : ClaimEvent(PersonId, ActorId);

/// <summary>A claimed profile's maintainer replied to a claim on it (AC-04.9).</summary>
public sealed record ClaimResponded(ClaimId ClaimId, PersonId PersonId, AccountId ActorId, bool HasResponse)
    : ClaimEvent(PersonId, ActorId);
