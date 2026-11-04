using FamilyGraph.Business.Claims;
using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Claims;

/// <summary>Loads and adds <see cref="ProfileClaim"/> aggregates (D-002). Reads go through <see cref="IClaimQueries"/>.</summary>
public interface IClaimStore
{
    Task<ProfileClaim?> FindAsync(ClaimId id, CancellationToken cancellationToken);

    /// <summary>The open claims on the profile, loaded for changes, oldest first.</summary>
    Task<IReadOnlyList<ProfileClaim>> ListOpenForPersonAsync(PersonId personId, CancellationToken cancellationToken);

    /// <summary>The account's most recent claim on the profile, in any state, or <see langword="null"/>.</summary>
    Task<ProfileClaim?> FindLatestAsync(PersonId personId, AccountId claimantId, CancellationToken cancellationToken);

    /// <summary>
    /// Whether the account already has a claimed, non-deleted profile or an open claim, which rules
    /// out another claim (D-051).
    /// </summary>
    Task<bool> AccountHasClaimAsync(AccountId accountId, CancellationToken cancellationToken);

    /// <summary>
    /// Up to <paramref name="limit"/> closed claims closed before <paramref name="closedBefore"/>
    /// whose notes are not yet erased, loaded for changes (AC-04.10).
    /// </summary>
    Task<IReadOnlyList<ProfileClaim>> ListNotesToEraseAsync(DateTimeOffset closedBefore, int limit, CancellationToken cancellationToken);

    void Add(ProfileClaim claim);
}
