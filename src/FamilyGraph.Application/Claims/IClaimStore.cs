using FamilyGraph.Business.Claims;
using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Claims;

/// <summary>Loads and adds <see cref="ProfileClaim"/> aggregates (D-002). Reads go through <see cref="IClaimQueries"/>.</summary>
public interface IClaimStore
{
    Task<ProfileClaim?> FindAsync(ClaimId id, CancellationToken cancellationToken);

    /// <summary>The open claims on the profile, loaded for changes.</summary>
    Task<IReadOnlyList<ProfileClaim>> ListOpenForPersonAsync(PersonId personId, CancellationToken cancellationToken);

    /// <summary>
    /// Whether the account already has a claimed, non-deleted profile or an open claim, which rules
    /// out another claim (D-051).
    /// </summary>
    Task<bool> AccountHasClaimAsync(AccountId accountId, CancellationToken cancellationToken);

    void Add(ProfileClaim claim);
}
