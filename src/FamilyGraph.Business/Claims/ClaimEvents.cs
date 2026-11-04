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
