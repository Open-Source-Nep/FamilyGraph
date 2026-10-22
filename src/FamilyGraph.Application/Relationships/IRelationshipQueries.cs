using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Relationships;

/// <summary>
/// Read models of parent–child links (D-002). Relationship views return only active links
/// (PRD 03); the consent view returns the inactive links of the viewer's own profiles (D-021).
/// </summary>
public interface IRelationshipQueries
{
    /// <summary>
    /// The person's parents, sorted by name, and children, in birth order with unordered children
    /// after them by name (D-016). Each list holds at most <paramref name="limit"/> people (D-010).
    /// </summary>
    Task<DirectRelatives> ListDirectRelativesAsync(
        PersonId personId, AccountId viewerId, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// The non-deleted, inactive links with at least one endpoint the viewer maintains, those
    /// waiting for the viewer's approval first, then by parent and child name. At most
    /// <paramref name="limit"/> links (D-010).
    /// </summary>
    Task<LinkConsentList> ListInactiveLinksAsync(AccountId viewerId, int limit, CancellationToken cancellationToken);
}
