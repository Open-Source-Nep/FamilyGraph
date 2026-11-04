using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.Claims;
using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Claims;

/// <summary>Read models of identity claims (D-002). Every list takes a limit (D-010).</summary>
public interface IClaimQueries
{
    /// <summary>
    /// The open claims the account may decide as maintainer: on unclaimed, non-deleted profiles it
    /// maintains that have no other open claim (AC-04.7, AC-04.9), oldest first.
    /// </summary>
    Task<ClaimRecords> ListToDecideAsync(AccountId maintainerId, int limit, CancellationToken cancellationToken);

    /// <summary>The account's own claims, newest first.</summary>
    Task<ClaimRecords> ListByClaimantAsync(AccountId claimantId, int limit, CancellationToken cancellationToken);
}

/// <summary>A claim with its person before privacy is applied, for <see cref="PrivacyProjection"/>.</summary>
public sealed record ClaimRecord(
    ClaimId Id,
    PersonRecord Person,
    AccountId ClaimantId,
    ClaimState State,
    string? Evidence,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? ClosedAt)
{
    public static ClaimRecord From(ProfileClaim claim, PersonRecord person)
    {
        ArgumentNullException.ThrowIfNull(claim);
        return new ClaimRecord(claim.Id, person, claim.ClaimantId, claim.State, claim.Evidence, claim.SubmittedAt, claim.ClosedAt);
    }
}

public sealed record ClaimRecords(IReadOnlyList<ClaimRecord> Claims, bool IsTruncated);
