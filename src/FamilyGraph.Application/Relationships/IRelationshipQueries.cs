using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Relationships;

/// <summary>Read models of parent–child links (D-002). Only active links are returned (PRD 03).</summary>
public interface IRelationshipQueries
{
    /// <summary>
    /// The person's parents, sorted by name, and children, in birth order with unordered children
    /// after them by name (D-016). Each list holds at most <paramref name="limit"/> people (D-010).
    /// </summary>
    Task<DirectRelatives> ListDirectRelativesAsync(PersonId personId, int limit, CancellationToken cancellationToken);
}
