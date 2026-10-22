using FamilyGraph.Business.People;

namespace FamilyGraph.Business.Relationships;

/// <summary>
/// The ancestors of one person through all non-deleted links (active, pending and inactive),
/// as found by a bounded query (PRD 02, D-009). <see cref="IsComplete"/> is false when the
/// query stopped at a limit before it ran out of ancestors.
/// </summary>
public sealed class AncestorSnapshot
{
    /// <summary>Most ancestors a cycle check examines (D-009).</summary>
    public const int MaxAncestors = 5_000;

    /// <summary>Most generations a cycle check walks up (D-009).</summary>
    public const int MaxGenerations = 50;

    public AncestorSnapshot(PersonId personId, IReadOnlySet<PersonId> ancestors, bool isComplete)
    {
        ArgumentNullException.ThrowIfNull(ancestors);

        PersonId = personId;
        Ancestors = ancestors;
        IsComplete = isComplete;
    }

    public PersonId PersonId { get; }

    public IReadOnlySet<PersonId> Ancestors { get; }

    public bool IsComplete { get; }
}
