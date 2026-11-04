using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.Claims;

namespace FamilyGraph.Application.Claims;

/// <summary>
/// A claim as one viewer may see it. <see cref="Evidence"/> is only for the claimant and the
/// maintainer deciding it (PRD 04, AC-04.5). It never names the claimant's account: the note is
/// how a claimant introduces themselves (D-051).
/// </summary>
public sealed record ClaimView(
    ClaimId Id,
    PersonReference Person,
    ClaimState State,
    string? Evidence,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? ClosedAt);

public sealed record ClaimList(IReadOnlyList<ClaimView> Claims, bool IsTruncated);

/// <summary>
/// The claim state of one profile for the viewer, for the person page.
/// <see cref="IsClaimed"/> is only told to the maintainer and administrators (and to the claimed person
/// through <see cref="ClaimedByYou"/>), so others do not learn who has an account.
/// </summary>
public sealed record ProfileClaimStatus(
    bool? IsClaimed,
    bool ClaimedByYou,
    bool YouHaveClaim,
    ClaimView? YourOpenClaim,
    IReadOnlyList<ClaimView> ToDecide,
    bool NeedsAdministrator);
