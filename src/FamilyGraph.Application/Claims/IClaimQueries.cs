using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.Claims;
using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Claims;

/// <summary>Read models of identity claims (D-002). Every list takes a limit (D-010).</summary>
public interface IClaimQueries
{
    /// <summary>
    /// The open claims the account may decide as maintainer: on unclaimed, non-deleted profiles it
    /// maintains that have no other open claim (AC-04.7, AC-04.9), and not sent to administrators
    /// (D-052), oldest first.
    /// </summary>
    Task<ClaimRecords> ListToDecideAsync(AccountId maintainerId, int limit, CancellationToken cancellationToken);

    /// <summary>The open claims on claimed, non-deleted profiles the account maintains, oldest first (AC-04.9).</summary>
    Task<ClaimRecords> ListToRespondAsync(AccountId maintainerId, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// The open claims waiting for administrators (D-052): sent to review, on a claimed profile, or
    /// on a profile with another open claim, oldest first.
    /// </summary>
    Task<ClaimRecords> ListForAdministratorsAsync(AccountId viewerId, int limit, CancellationToken cancellationToken);

    /// <summary>The account's own claims, newest first.</summary>
    Task<ClaimRecords> ListByClaimantAsync(AccountId claimantId, int limit, CancellationToken cancellationToken);
}

/// <summary>
/// A claim with its person before privacy is applied, for <see cref="PrivacyProjection"/>, and
/// what decides who reviews it: whether the profile is deleted or claimed and how many open claims
/// it has.
/// </summary>
public sealed record ClaimRecord(
    ProfileClaim Claim,
    PersonRecord Person,
    bool ProfileClaimed,
    int OpenClaimsOnProfile)
{
    public static ClaimRecord From(ProfileClaim claim, Person person, AccountId viewerId, int openClaimsOnProfile)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(person);
        return new ClaimRecord(claim, PersonRecord.From(person, viewerId), person.IsClaimed, openClaimsOnProfile);
    }
}

public sealed record ClaimRecords(IReadOnlyList<ClaimRecord> Claims, bool IsTruncated);
