using System.ComponentModel.DataAnnotations;
using FamilyGraph.Business.Claims;

namespace Server.Claims;

/// <summary>A claim's optional note: who you are and who can confirm it (D-051).</summary>
public sealed class SubmitClaimRequest
{
    [MaxLength(ClaimEvidence.MaxLength + 100)]
    public string? Evidence { get; init; }
}
