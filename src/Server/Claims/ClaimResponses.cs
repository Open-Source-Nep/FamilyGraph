using FamilyGraph.Application.Claims;
using Server.People;

namespace Server.Claims;

/// <summary>
/// A claim as you may see it: <c>state</c> is <c>Open</c>, <c>Approved</c>, <c>Rejected</c>,
/// <c>Withdrawn</c> or <c>Cancelled</c>. The note is only sent to the claimant and the deciding maintainer.
/// </summary>
public sealed record ClaimResponse(
    Guid Id,
    PersonResponse Person,
    string State,
    string? Evidence,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? ClosedAt)
{
    internal static ClaimResponse From(ClaimView claim) =>
        new(claim.Id.Value, PersonResponse.From(claim.Person), claim.State.Name, claim.Evidence, claim.SubmittedAt, claim.ClosedAt);
}

public sealed record ClaimListResponse(IReadOnlyList<ClaimResponse> Claims, bool IsTruncated)
{
    internal static ClaimListResponse From(ClaimList list) => new([.. list.Claims.Select(ClaimResponse.From)], list.IsTruncated);
}

/// <summary>
/// One profile's claim state for you. <c>claimed</c> is <see langword="null"/> unless you maintain
/// the profile or are an administrator. <c>youHaveClaim</c> says your account already has a claimed
/// profile or an open claim, so you cannot claim another. <c>needsAdministrator</c> tells the
/// maintainer that open claims on the profile are an administrator's to decide.
/// </summary>
public sealed record ProfileClaimStatusResponse(
    bool? Claimed,
    bool ClaimedByYou,
    bool YouHaveClaim,
    ClaimResponse? YourClaim,
    IReadOnlyList<ClaimResponse> ToDecide,
    bool NeedsAdministrator)
{
    internal static ProfileClaimStatusResponse From(ProfileClaimStatus status) =>
        new(
            status.IsClaimed,
            status.ClaimedByYou,
            status.YouHaveClaim,
            status.YourOpenClaim is null ? null : ClaimResponse.From(status.YourOpenClaim),
            [.. status.ToDecide.Select(ClaimResponse.From)],
            status.NeedsAdministrator);
}
