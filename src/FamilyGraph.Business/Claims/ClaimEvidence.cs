namespace FamilyGraph.Business.Claims;

/// <summary>
/// The claimant's evidence (PRD 04, D-051): one optional free-text note of at most
/// <see cref="MaxLength"/> characters. It is personal data, so no audit event holds it (D-015).
/// </summary>
public static class ClaimEvidence
{
    public const int MaxLength = 2000;

    /// <summary>The trimmed note, or <see langword="null"/> when blank.</summary>
    public static string? Normalize(string? evidence) =>
        ClaimText.Optional(evidence, MaxLength, () => new ClaimEvidenceTooLongException());
}

/// <summary>
/// The other texts of a claim (D-052): an administrator's rationale and question, and the reply of a
/// claimed profile's maintainer. Each has at most <see cref="MaxLength"/> characters. None is in an
/// audit event (D-015).
/// </summary>
public static class ClaimText
{
    public const int MaxLength = ClaimEvidence.MaxLength;

    /// <summary>An administrator's rationale for a decision: required.</summary>
    public static string Rationale(string? rationale) =>
        Optional(rationale, MaxLength, () => new ClaimTextTooLongException("claim.rationale_too_long"))
        ?? throw new ClaimTextRequiredException("claim.rationale_required");

    /// <summary>An administrator's question to the claimant: required.</summary>
    public static string Question(string? question) =>
        Optional(question, MaxLength, () => new ClaimTextTooLongException("claim.question_too_long"))
        ?? throw new ClaimTextRequiredException("claim.question_required");

    /// <summary>A claimed profile's maintainer's reply: optional, blank clears it.</summary>
    public static string? Response(string? response) =>
        Optional(response, MaxLength, () => new ClaimTextTooLongException("claim.response_too_long"));

    internal static string? Optional(string? text, int maxLength, Func<DomainException> tooLong)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();
        return trimmed.Length > maxLength ? throw tooLong() : trimmed;
    }
}

public sealed class ClaimEvidenceTooLongException()
    : DomainException("claim.evidence_too_long", $"A claim's note has at most {ClaimEvidence.MaxLength} characters.");

public sealed class ClaimTextTooLongException(string code)
    : DomainException(code, $"This text has at most {ClaimText.MaxLength} characters.");

public sealed class ClaimTextRequiredException(string code)
    : DomainException(code, "This text is required.");
