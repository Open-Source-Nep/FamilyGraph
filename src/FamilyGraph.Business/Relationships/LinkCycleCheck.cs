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
}

public sealed class SelfLinkException()
    : DomainException("link.self_link", "A person cannot be their own parent.");

public sealed class LinkCycleDetectedException()
    : DomainException("link.cycle_detected", "The proposed child is already an ancestor of the proposed parent.");

public sealed class LinkCycleUnverifiableException()
    : DomainException(
        "link.cycle_unverifiable",
        "The link could not be verified as cycle-free because the ancestor check reached its limit.");
