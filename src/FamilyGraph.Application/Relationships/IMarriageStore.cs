using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Application.Relationships;

/// <summary>Loads what <see cref="Marriage"/> needs to check a change, and adds marriages (D-002, D-031).</summary>
public interface IMarriageStore
{
    /// <summary>The marriage, deleted or not, for a change; <see langword="null"/> if it does not exist.</summary>
    Task<Marriage?> FindAsync(LinkId id, CancellationToken cancellationToken);

    /// <summary>Whether the two people already have a non-deleted marriage, in any consent state.</summary>
    Task<bool> ExistsAsync(PersonId spouseId, PersonId otherSpouseId, CancellationToken cancellationToken);

    /// <summary>Whether one person is the other's parent through a non-deleted link, in any consent state.</summary>
    Task<bool> AreParentAndChildAsync(PersonId personId, PersonId otherPersonId, CancellationToken cancellationToken);

    void Add(Marriage marriage);
}
