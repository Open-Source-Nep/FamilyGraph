using System.ComponentModel.DataAnnotations;

namespace Server.Relationships;

/// <summary>A new marriage between two people. The order of the spouses does not matter.</summary>
public sealed class CreateMarriageRequest
{
    [Required]
    public Guid? SpouseId { get; init; }

    [Required]
    public Guid? OtherSpouseId { get; init; }
}

/// <summary>The spouse whose side to approve or disagree for.</summary>
public sealed class MarriageConsentRequest
{
    [Required]
    public Guid? SpouseId { get; init; }
}

/// <summary>Whether the marriage has ended (<see langword="true"/>) or is current again.</summary>
public sealed class MarriageFormerRequest
{
    [Required]
    public bool? IsFormer { get; init; }
}
