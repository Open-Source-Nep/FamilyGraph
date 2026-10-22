using FamilyGraph.Business.People;

namespace FamilyGraph.Business.Relationships;

/// <summary>
/// Sets the birth order of one parent's children (PRD 02, D-016). The order is a manual position
/// on each of the parent's links and is used only to sort children for display. Only the parent's
/// maintainer may change it, and it never changes consent or whether a link is active.
/// </summary>
public static class ChildBirthOrder
{
    /// <summary>
    /// Places the children of <paramref name="eldestFirst"/> at positions 1, 2, … in that order.
    /// The parent's other links become unordered.
    /// </summary>
    /// <param name="parentLinks">All non-deleted links of <paramref name="parent"/> as parent.</param>
    /// <param name="eldestFirst">The links to place, eldest child first.</param>
    public static void Arrange(
        Person parent,
        AccountId actorId,
        IReadOnlyCollection<ParentChildLink> parentLinks,
        IReadOnlyList<LinkId> eldestFirst)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(parentLinks);
        ArgumentNullException.ThrowIfNull(eldestFirst);

        if (parentLinks.Any(link => link.ParentId != parent.Id || link.IsDeleted))
        {
            throw new ArgumentException("The links must be the parent's non-deleted links.", nameof(parentLinks));
        }

        if (!parent.IsMaintainedBy(actorId))
        {
            throw new NotParentMaintainerException();
        }

        var linksById = parentLinks.ToDictionary(link => link.Id);
        var positions = new Dictionary<LinkId, int>();
        foreach (var linkId in eldestFirst)
        {
            if (!linksById.ContainsKey(linkId))
            {
                throw new BirthOrderUnknownChildException();
            }

            if (!positions.TryAdd(linkId, positions.Count + 1))
            {
                throw new BirthOrderDuplicateChildException();
            }
        }

        int? PositionOf(ParentChildLink link) => positions.TryGetValue(link.Id, out var position) ? position : null;

        if (parentLinks.All(link => link.BirthOrder == PositionOf(link)))
        {
            throw new BirthOrderUnchangedException();
        }

        foreach (var link in parentLinks)
        {
            link.SetBirthOrder(PositionOf(link));
        }
    }
}

public sealed class NotParentMaintainerException()
    : DomainException("link.birth_order_not_parent_maintainer", "Only the parent's maintainer can change the order of their children.");

public sealed class BirthOrderUnknownChildException()
    : DomainException("link.birth_order_unknown_child", "The order includes a child who is not linked to this parent.");

public sealed class BirthOrderDuplicateChildException()
    : DomainException("link.birth_order_duplicate_child", "The order lists the same child more than once.");

public sealed class BirthOrderUnchangedException()
    : DomainException("link.birth_order_unchanged", "The children are already in this order.");
