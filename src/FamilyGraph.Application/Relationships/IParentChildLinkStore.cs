using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Application.Relationships;

/// <summary>
/// Loads what <see cref="ParentChildLink"/> needs to check a change, and adds links (D-002).
/// Both lookups count every non-deleted link: active, pending and inactive (PRD 02).
/// </summary>
public interface IParentChildLinkStore
{
    /// <summary>The link, deleted or not, for a change; <see langword="null"/> if it does not exist.</summary>
    Task<ParentChildLink?> FindAsync(LinkId id, CancellationToken cancellationToken);

    /// <summary>The parents of <paramref name="childId"/> through non-deleted links.</summary>
    Task<IReadOnlySet<PersonId>> ParentsOfAsync(PersonId childId, CancellationToken cancellationToken);

    /// <summary>
    /// The ancestors of <paramref name="personId"/> through non-deleted links, at most
    /// <see cref="AncestorSnapshot.MaxAncestors"/> of them and <see cref="AncestorSnapshot.MaxGenerations"/>
    /// generations up. The snapshot is incomplete when the search stopped at either limit (D-009).
    /// </summary>
    Task<AncestorSnapshot> AncestorsOfAsync(PersonId personId, CancellationToken cancellationToken);

    void Add(ParentChildLink link);
}
