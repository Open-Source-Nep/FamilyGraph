using FamilyGraph.Business.People;

namespace FamilyGraph.Business.Relationships;

/// <summary>
/// Rejects a parent → child link that would make someone their own ancestor (PRD 02).
/// Used when a link is created and when it is reactivated.
/// </summary>
public static class LinkCycleCheck
{
    /// <param name="parentAncestors">The ancestors of <paramref name="parentId"/>.</param>
    public static void EnsureNoCycle(PersonId parentId, PersonId childId, AncestorSnapshot parentAncestors)
    {
        ArgumentNullException.ThrowIfNull(parentAncestors);

        if (parentAncestors.PersonId != parentId)
        {
            throw new ArgumentException("The ancestor snapshot must be of the proposed parent.", nameof(parentAncestors));
        }

        if (parentId == childId)
        {
            throw new SelfLinkException();
        }

        if (parentAncestors.Ancestors.Contains(childId))
        {
            throw new LinkCycleDetectedException();
        }

        if (!parentAncestors.IsComplete)
        {
            throw new LinkCycleUnverifiableException();
        }
    }

    /// <summary>
    /// Whether a whole set of links makes someone their own ancestor, for an import that has every
    /// link at once (D-033). It removes people without a remaining parent until none is left; any
    /// link that remains is on or below a cycle.
    /// </summary>
    public static bool HasCycle(IEnumerable<(PersonId ParentId, PersonId ChildId)> links)
    {
        ArgumentNullException.ThrowIfNull(links);

        var childrenOf = new Dictionary<PersonId, List<PersonId>>();
        var parentCount = new Dictionary<PersonId, int>();
        var linkCount = 0;
        foreach (var (parentId, childId) in links)
        {
            if (parentId == childId)
            {
                return true;
            }

            if (!childrenOf.TryGetValue(parentId, out var children))
            {
                childrenOf[parentId] = children = [];
            }

            children.Add(childId);
            parentCount[childId] = parentCount.GetValueOrDefault(childId) + 1;
            linkCount++;
        }

        var roots = new Queue<PersonId>(childrenOf.Keys.Where(person => !parentCount.ContainsKey(person)));
        while (roots.TryDequeue(out var person))
        {
            foreach (var child in childrenOf.GetValueOrDefault(person) ?? [])
            {
                linkCount--;
                if (--parentCount[child] == 0)
                {
                    roots.Enqueue(child);
                }
            }
        }

        return linkCount > 0;
    }
}

public sealed class SelfLinkException()
    : DomainException("link.self_link", "A person cannot be their own parent.");

public sealed class LinkCycleDetectedException()
    : DomainException("link.cycle_detected", "The proposed child is already an ancestor of the proposed parent.");

public sealed class LinkCycleUnverifiableException()
    : DomainException(
        "link.cycle_unverifiable",
        "The link could not be verified as cycle-free because the ancestor check reached its limit.");
