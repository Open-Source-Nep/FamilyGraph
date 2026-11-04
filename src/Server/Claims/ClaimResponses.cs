using FamilyGraph.Application.Claims;
using Server.People;

namespace Server.Claims;

/// <summary>
/// A claim as you may see it (AC-04.5, D-052): <c>state</c> is <c>Open</c>, <c>Approved</c>,
/// <c>Rejected</c>, <c>Withdrawn</c> or <c>Cancelled</c>. <c>withAdministrators</c> says an open
/// claim waits for administrators. The note is sent to the claimant, the deciding maintainer and
/// administrators; the administrator's question to the claimant and administrators; the claimed
/// person's reply to that maintainer and administrators; the rationale, <c>profileClaimed</c> and
/// <c>openClaimsOnProfile</c> to administrators only. <c>mayRequestReview</c> tells the claimant they
/// may ask administrators now, and <c>reviewAvailableAt</c> from when, while the maintainer decides.
/// </summary>
public sealed record ClaimResponse(
    Guid Id,
    PersonResponse Person,
    string State,
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
    int? OpenClaimsOnProfile)
{
    internal static ClaimResponse From(ClaimView claim) =>
        new(
            claim.Id.Value,
            PersonResponse.From(claim.Person),
            claim.State.Name,
            claim.Evidence,
            claim.SubmittedAt,
            claim.ClosedAt,
            claim.WithAdministrators,
            claim.Appealed,
            claim.DecidedByAdministrator,
            claim.AdministratorQuestion,
            claim.MaintainerResponse,
            claim.Rationale,
            claim.MayRequestReview,
            claim.ReviewAvailableAt,
            claim.ProfileClaimed,
            claim.OpenClaimsOnProfile);
}

public sealed record ClaimListResponse(IReadOnlyList<ClaimResponse> Claims, bool IsTruncated)
{
    internal static ClaimListResponse From(ClaimList list) => new([.. list.Claims.Select(ClaimResponse.From)], list.IsTruncated);
}

/// <summary>
/// One profile's claim state for you. <c>claimed</c> is <see langword="null"/> unless you maintain
/// the profile or are an administrator. <c>youHaveClaim</c> says your account already has a claimed
/// profile or an open claim, so you cannot claim another. <c>yourClaim</c> is your latest claim on
/// the profile, open or closed, and <c>claimAgainFrom</c> when you may claim it again after an
/// administrator's rejection. <c>toRespond</c> are the open claims on the claimed profile you
/// maintain, for a reply. <c>needsAdministrator</c> tells the maintainer that open claims on the
/// profile are an administrator's to decide.
/// </summary>
public sealed record ProfileClaimStatusResponse(
    bool? Claimed,
    bool ClaimedByYou,
    bool YouHaveClaim,
    ClaimResponse? YourClaim,
    DateTimeOffset? ClaimAgainFrom,
    IReadOnlyList<ClaimResponse> ToDecide,
    IReadOnlyList<ClaimResponse> ToRespond,
    bool NeedsAdministrator)
{
    internal static ProfileClaimStatusResponse From(ProfileClaimStatus status) =>
        new(
            status.IsClaimed,
            status.ClaimedByYou,
            status.YouHaveClaim,
            status.YourClaim is null ? null : ClaimResponse.From(status.YourClaim),
            status.ClaimAgainFrom,
            [.. status.ToDecide.Select(ClaimResponse.From)],
            [.. status.ToRespond.Select(ClaimResponse.From)],
            status.NeedsAdministrator);
}
