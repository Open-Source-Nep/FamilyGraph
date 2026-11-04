namespace FamilyGraph.Business.Claims;

/// <summary>
/// The claimant's evidence (PRD 04, D-051): one optional free-text note of at most
/// <see cref="MaxLength"/> characters. It is personal data, so no audit event holds it (D-015).
/// </summary>
public static class ClaimEvidence
{
    public const int MaxLength = 2000;

    /// <summary>The trimmed note, or <see langword="null"/> when blank.</summary>
    public static string? Normalize(string? evidence)
    {
        if (string.IsNullOrWhiteSpace(evidence))
        {
            return null;
        }

        var trimmed = evidence.Trim();
        return trimmed.Length > MaxLength ? throw new ClaimEvidenceTooLongException() : trimmed;
    }
}

public sealed class ClaimEvidenceTooLongException()
    : DomainException("claim.evidence_too_long", $"A claim's note has at most {ClaimEvidence.MaxLength} characters.");
