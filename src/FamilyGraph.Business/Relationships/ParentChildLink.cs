using FamilyGraph.Business.People;

namespace FamilyGraph.Business.Relationships;

/// <summary>
/// A directly registered parent–child fact (PRD 02). Consent is tracked per endpoint (PRD 03):
/// the link is active only when both the parent side and the child side have approved.
/// </summary>
public sealed class ParentChildLink
{
    private ParentChildLink(
        LinkId id,
        PersonId parentId,
        PersonId childId,
        ParentChildType type,
        EndpointApproval parentApproval,
        EndpointApproval childApproval)
    {
        Id = id;
        ParentId = parentId;
        ChildId = childId;
        Type = type;
        ParentApproval = parentApproval;
        ChildApproval = childApproval;
    }

    public LinkId Id { get; }

    public PersonId ParentId { get; }

    public PersonId ChildId { get; }

    public ParentChildType Type { get; }

    public EndpointApproval ParentApproval { get; private set; }

    public EndpointApproval ChildApproval { get; private set; }

    public bool IsActive => ParentApproval == EndpointApproval.Approved && ChildApproval == EndpointApproval.Approved;

    /// <summary>
    /// Creates a link. The creator must maintain at least one endpoint; their endpoint(s) start
    /// approved and an endpoint maintained by another account starts pending (PRD 03).
    /// </summary>
    /// <param name="childParents">The child's current parents through non-deleted links.</param>
    /// <param name="parentAncestors">The ancestors of <paramref name="parent"/> through non-deleted links.</param>
    public static ParentChildLink Create(
        Person parent,
        Person child,
        ParentChildType type,
        AccountId creatorId,
        IReadOnlySet<PersonId> childParents,
        AncestorSnapshot parentAncestors)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(child);
        ArgumentNullException.ThrowIfNull(childParents);

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown parent–child type.");
        }

        var maintainsParent = parent.IsMaintainedBy(creatorId);
        var maintainsChild = child.IsMaintainedBy(creatorId);
        if (!maintainsParent && !maintainsChild)
        {
            throw new NotLinkEndpointMaintainerException();
        }

        if (childParents.Contains(parent.Id))
        {
            throw new DuplicateLinkException();
        }

        LinkCycleCheck.EnsureNoCycle(parent.Id, child.Id, parentAncestors);

        return new ParentChildLink(
            LinkId.New(),
            parent.Id,
            child.Id,
            type,
            maintainsParent ? EndpointApproval.Approved : EndpointApproval.Pending,
            maintainsChild ? EndpointApproval.Approved : EndpointApproval.Pending);
    }
}

public sealed class NotLinkEndpointMaintainerException()
    : DomainException("link.not_endpoint_maintainer", "Only the maintainer of the parent or the child can create a link.");

public sealed class DuplicateLinkException()
    : DomainException("link.duplicate", "A link between this parent and this child already exists.");
