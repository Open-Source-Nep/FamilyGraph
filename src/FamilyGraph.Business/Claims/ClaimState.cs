namespace FamilyGraph.Business.Claims;

/// <summary>Where an identity claim stands (PRD 04, D-051).</summary>
public sealed class ClaimState : BaseEnum<ClaimState>
{
    /// <summary>Waiting for a decision.</summary>
    public static readonly ClaimState Open = new(1, nameof(Open));

    /// <summary>The claimant became the profile's maintainer and the person it describes.</summary>
    public static readonly ClaimState Approved = new(2, nameof(Approved));

    public static readonly ClaimState Rejected = new(3, nameof(Rejected));

    /// <summary>The claimant took the claim back before a decision.</summary>
    public static readonly ClaimState Withdrawn = new(4, nameof(Withdrawn));

    /// <summary>The profile was deleted before a decision.</summary>
    public static readonly ClaimState Cancelled = new(5, nameof(Cancelled));

    private ClaimState(int value, string name)
        : base(value, name)
    {
    }
}
