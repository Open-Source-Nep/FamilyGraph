using System.ComponentModel.DataAnnotations;
using FamilyGraph.Business.Claims;

namespace Server.Claims;

/// <summary>A claim's optional note: who you are and who can confirm it (D-051).</summary>
public sealed class SubmitClaimRequest
{
    [MaxLength(ClaimEvidence.MaxLength + 100)]
    public string? Evidence { get; init; }
}

/// <summary>The claim's new note; blank removes it.</summary>
public sealed class ChangeClaimEvidenceRequest
{
    [MaxLength(ClaimEvidence.MaxLength + 100)]
    public string? Evidence { get; init; }
}

/// <summary>A claimed profile's maintainer's reply to a claim on it; blank removes it (AC-04.9).</summary>
public sealed class ClaimResponseRequest
{
    [MaxLength(ClaimText.MaxLength + 100)]
    public string? Response { get; init; }
}

/// <summary>An administrator's required reason for a decision (D-052).</summary>
public sealed class ClaimDecisionRequest
{
    [MaxLength(ClaimText.MaxLength + 100)]
    public string? Rationale { get; init; }
}

/// <summary>An administrator's question to the claimant (D-052).</summary>
public sealed class ClaimQuestionRequest
{
    [MaxLength(ClaimText.MaxLength + 100)]
    public string? Question { get; init; }
}
