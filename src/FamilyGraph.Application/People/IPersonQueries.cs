using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.People;

namespace FamilyGraph.Application.People;

/// <summary>Read models of person profiles (D-002). Every list takes a limit (D-010).</summary>
public interface IPersonQueries
{
    /// <summary>The person before privacy is applied, for <see cref="PrivacyProjection"/>.</summary>
    Task<PersonRecord?> FindAsync(PersonId id, AccountId viewerId, CancellationToken cancellationToken);

    /// <summary>The profiles the account maintains, sorted by name, at most <paramref name="limit"/> of them.</summary>
    Task<ProfileList> ListMaintainedByAsync(AccountId maintainerId, int limit, CancellationToken cancellationToken);
}
