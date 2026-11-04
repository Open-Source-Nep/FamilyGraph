namespace FamilyGraph.Application.Claims;

/// <summary>
/// Claim settings (D-051, D-052): how many claims a list returns (<c>Claims:ListLimit</c>, D-010),
/// how long a maintainer has to answer before the claimant may ask for review
/// (<c>Claims:MaintainerResponseDays</c>, AC-04.6), how long a claimant waits after an
/// administrator's rejection (<c>Claims:NewClaimWaitDays</c>, AC-04.8) and how long a closed
/// claim's notes are kept (<c>Claims:EvidenceRetentionDays</c>, AC-04.10).
/// </summary>
public sealed record ClaimLimits(
    int ListLimit = ClaimLimits.DefaultListLimit,
    int MaintainerResponseDays = ClaimLimits.DefaultMaintainerResponseDays,
    int NewClaimWaitDays = ClaimLimits.DefaultNewClaimWaitDays,
    int EvidenceRetentionDays = ClaimLimits.DefaultEvidenceRetentionDays)
{
    public const int DefaultListLimit = 100;

    public const int DefaultMaintainerResponseDays = 30;

    public const int DefaultNewClaimWaitDays = 90;

    public const int DefaultEvidenceRetentionDays = 90;

    public TimeSpan MaintainerResponseTime => TimeSpan.FromDays(MaintainerResponseDays);

    public TimeSpan NewClaimWait => TimeSpan.FromDays(NewClaimWaitDays);

    public TimeSpan EvidenceRetention => TimeSpan.FromDays(EvidenceRetentionDays);
}
