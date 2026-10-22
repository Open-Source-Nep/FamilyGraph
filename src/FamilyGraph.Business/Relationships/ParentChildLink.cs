using FamilyGraph.Business.Audit;
using FamilyGraph.Business.People;

namespace FamilyGraph.Business.Relationships;

/// <summary>
/// A directly registered parent–child fact (PRD 02). Consent is tracked per endpoint (PRD 03):
/// the link is active only when both the parent side and the child side have approved.
/// Each endpoint's maintainer moves that endpoint between states with <see cref="Approve"/> and
/// <see cref="Disagree"/> (D-013); no one else can change it. An account that maintains both
/// endpoints removes the link with <see cref="Delete"/> instead (D-014).
/// </summary>
public sealed class ParentChildLink : AuditedEntity
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

    /// <summary>When the link was deleted, or <see langword="null"/> while it exists (D-009).</summary>
    public DateTimeOffset? DeletedAt { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    /// <summary>
    /// The child's birth-order number among the parent's children (lower is elder), or
    /// <see langword="null"/> while the parent's maintainer has not placed the child (D-016).
    /// It only sorts children for display and never affects consent.
    /// </summary>
    public int? BirthOrder { get; private set; }

    public bool IsActive =>
        !IsDeleted && ParentApproval == EndpointApproval.Approved && ChildApproval == EndpointApproval.Approved;

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
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(childParents);

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

        var link = new ParentChildLink(
            LinkId.New(),
            parent.Id,
            child.Id,
            type,
            maintainsParent ? EndpointApproval.Approved : EndpointApproval.Pending,
            maintainsChild ? EndpointApproval.Approved : EndpointApproval.Pending);
        link.Record(new LinkCreated(
            link.Id, link.ParentId, link.ChildId, link.Type, link.ParentApproval, link.ChildApproval, creatorId));
        return link;
    }

    /// <summary>
    /// Approves the link for <paramref name="endpoint"/>, from pending or from disagreed (restoring
    /// approval). The other side's valid approval is kept, so this can reactivate the link; if it
    /// does, the cycle check runs again first (PRD 02).
    /// </summary>
    /// <param name="parentAncestors">The ancestors of the parent through non-deleted links.</param>
    public void Approve(Person endpoint, AccountId actorId, AncestorSnapshot parentAncestors)
    {
        ArgumentNullException.ThrowIfNull(parentAncestors);

        var isParentSide = IsParentSide(endpoint, actorId);
        EnsureNotDeleted();
        if (ApprovalOf(isParentSide) == EndpointApproval.Approved)
        {
            throw new ConsentUnchangedException();
        }

        if (ApprovalOf(!isParentSide) == EndpointApproval.Approved)
        {
            LinkCycleCheck.EnsureNoCycle(ParentId, ChildId, parentAncestors);
        }

        var wasDisagreed = ApprovalOf(isParentSide) == EndpointApproval.Disagreed;
        SetApproval(isParentSide, EndpointApproval.Approved);
        Record(wasDisagreed
            ? new LinkApprovalRestored(Id, endpoint.Id, actorId)
            : new LinkEndpointApproved(Id, endpoint.Id, actorId));
    }

    /// <summary>
    /// Records disagreement for <paramref name="endpoint"/>, from approved or from pending
    /// (declining). The link becomes inactive immediately. When one account maintains both
    /// endpoints it must delete the link instead (#16).
    /// </summary>
    public void Disagree(Person endpoint, Person otherEndpoint, AccountId actorId)
    {
        ArgumentNullException.ThrowIfNull(otherEndpoint);

        var isParentSide = IsParentSide(endpoint, actorId);
        EnsureNotDeleted();
        if (otherEndpoint.Id != (isParentSide ? ChildId : ParentId))
        {
            throw new ArgumentException("The other endpoint must be the other person on this link.", nameof(otherEndpoint));
        }

        if (otherEndpoint.IsMaintainedBy(actorId))
        {
            throw new SoleMaintainerMustDeleteException();
        }

        if (ApprovalOf(isParentSide) == EndpointApproval.Disagreed)
        {
            throw new ConsentUnchangedException();
        }

        var wasPending = ApprovalOf(isParentSide) == EndpointApproval.Pending;
        SetApproval(isParentSide, EndpointApproval.Disagreed);
        Record(wasPending
            ? new LinkEndpointDeclined(Id, endpoint.Id, actorId)
            : new LinkEndpointDisagreed(Id, endpoint.Id, actorId));
    }

    /// <summary>
    /// Deletes the link, in any consent state. Only an account that maintains both endpoints may
    /// do this; anyone else withdraws their own approval with <see cref="Disagree"/> (PRD 03, D-014).
    /// Deletion is final and only sets <see cref="DeletedAt"/>, so the link's history is kept.
    /// </summary>
    public void Delete(Person parent, Person child, AccountId actorId, DateTimeOffset deletedAt)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(child);

        if (parent.Id != ParentId || child.Id != ChildId)
        {
            throw new ArgumentException("The parent and child must be the people on this link.");
        }

        if (!parent.IsMaintainedBy(actorId) || !child.IsMaintainedBy(actorId))
        {
            throw new NotSoleMaintainerException();
        }

        EnsureNotDeleted();

        DeletedAt = deletedAt;
        Record(new LinkDeleted(Id, deletedAt, actorId));
    }

    /// <summary>
    /// Sets the child's birth-order number, or clears it with <see langword="null"/>. Only the
    /// parent's maintainer may do this; it needs no consent and never changes the link's
    /// consent state or activity (PRD 02, D-016).
    /// </summary>
    public void SetBirthOrder(Person parent, AccountId actorId, int? birthOrder)
    {
        ArgumentNullException.ThrowIfNull(parent);

        if (parent.Id != ParentId)
        {
            throw new ArgumentException("The person must be the parent on this link.", nameof(parent));
        }

        if (!parent.IsMaintainedBy(actorId))
        {
            throw new NotParentMaintainerException();
        }

        EnsureNotDeleted();

        if (birthOrder is < 1)
        {
            throw new InvalidBirthOrderException();
        }

        if (birthOrder == BirthOrder)
        {
            throw new BirthOrderUnchangedException();
        }

        BirthOrder = birthOrder;
        Record(new LinkBirthOrderChanged(Id, ParentId, birthOrder, actorId));
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
        {
            throw new LinkDeletedException();
        }
    }

    /// <summary>Checks that the actor maintains the endpoint and tells which side it is.</summary>
    private bool IsParentSide(Person endpoint, AccountId actorId)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        if (endpoint.Id != ParentId && endpoint.Id != ChildId)
        {
            throw new ArgumentException("The person is not an endpoint of this link.", nameof(endpoint));
        }

        if (!endpoint.IsMaintainedBy(actorId))
        {
            throw new NotConsentMaintainerException();
        }

        return endpoint.Id == ParentId;
    }

    private EndpointApproval ApprovalOf(bool parentSide) => parentSide ? ParentApproval : ChildApproval;

    private void SetApproval(bool parentSide, EndpointApproval approval)
    {
        if (parentSide)
        {
            ParentApproval = approval;
        }
        else
        {
            ChildApproval = approval;
        }
    }
}

public sealed class NotLinkEndpointMaintainerException()
    : NotPermittedException("link.not_endpoint_maintainer", "Only the maintainer of the parent or the child can create a link.");

public sealed class DuplicateLinkException()
    : ConflictException("link.duplicate", "A link between this parent and this child already exists.");

public sealed class NotConsentMaintainerException()
    : NotPermittedException("link.consent_not_maintainer", "Only the maintainer of this endpoint can change its approval.");

public sealed class ConsentUnchangedException()
    : ConflictException("link.consent_unchanged", "This endpoint is already in the requested approval state.");

public sealed class SoleMaintainerMustDeleteException()
    : DomainException(
        "link.sole_maintainer_must_delete",
        "You maintain both people on this link, so delete the link instead of disagreeing with it.");

public sealed class NotSoleMaintainerException()
    : DomainException(
        "link.not_sole_maintainer",
        "Only someone who maintains both people on this link can delete it. Withdraw your approval instead.");

public sealed class NotParentMaintainerException()
    : DomainException("link.birth_order_not_parent_maintainer", "Only the parent's maintainer can change the order of their children.");

public sealed class InvalidBirthOrderException()
    : DomainException("link.birth_order_invalid", "A birth-order number must be 1 or greater.");

public sealed class BirthOrderUnchangedException()
    : DomainException("link.birth_order_unchanged", "The child already has this birth-order number.");

public sealed class LinkDeletedException()
    : DomainException("link.deleted", "This link has been deleted.");
