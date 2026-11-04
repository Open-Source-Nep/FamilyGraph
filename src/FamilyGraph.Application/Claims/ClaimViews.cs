using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.Claims;

namespace FamilyGraph.Application.Claims;

/// <summary>
/// A claim as one viewer may see it (PRD 04, AC-04.5, D-052). <see cref="Evidence"/> is for the
/// claimant, the maintainer deciding it and administrators; <see cref="AdministratorQuestion"/> for
/// the claimant and administrators; <see cref="MaintainerResponse"/> for the replying maintainer and
/// administrators; <see cref="Rationale"/>, <see cref="ProfileClaimed"/> and
/// <see cref="OpenClaimsOnProfile"/> for administrators only. It never names the claimant's account:
/// the note is how a claimant introduces themselves (D-051).
/// </summary>
public sealed record ClaimView(
    ClaimId Id,
    PersonReference Person,
    ClaimState State,
    string? Evidence,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? ClosedAt,
    bool WithAdministrators,
    bool Appealed,
    bool DecidedByAdministrator,
    string? AdministratorQuestion,
    string? MaintainerResponse,
    string? Rationale,
    bool MayRequestReview,
    DateTimeOffset? ReviewAvailableAt,
    bool? ProfileClaimed,
    int? OpenClaimsOnProfile);

public sealed record ClaimList(IReadOnlyList<ClaimView> Claims, bool IsTruncated);

/// <summary>
/// The claim state of one profile for the viewer, for the person page.
/// <see cref="IsClaimed"/> is only told to the maintainer and administrators (and to the claimed person
/// through <see cref="ClaimedByYou"/>), so others do not learn who has an account.
/// <see cref="YourClaim"/> is the viewer's latest claim on the profile, open or closed;
/// <see cref="ClaimAgainFrom"/> is when they may claim it again after an administrator's rejection.
/// <see cref="ToRespond"/> are the open claims on the claimed profile the viewer maintains (AC-04.9).
/// </summary>
public sealed record ProfileClaimStatus(
    bool? IsClaimed,
    bool ClaimedByYou,
    bool YouHaveClaim,
    ClaimView? YourClaim,
    DateTimeOffset? ClaimAgainFrom,
    IReadOnlyList<ClaimView> ToDecide,
    IReadOnlyList<ClaimView> ToRespond,
    bool NeedsAdministrator);
