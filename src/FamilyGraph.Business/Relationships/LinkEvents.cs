using FamilyGraph.Business.Audit;
using FamilyGraph.Business.People;

namespace FamilyGraph.Business.Relationships;

/// <summary>A relationship-consent event about one link (PRD 07, D-015).</summary>
public abstract record LinkEvent(LinkId LinkId, AccountId ActorId) : AuditEvent(ActorId)
{
    public override AuditFamily Family => AuditFamily.RelationshipConsent;
}

public sealed record LinkCreated(
    LinkId LinkId,
    PersonId ParentId,
    PersonId ChildId,
    ParentChildType Type,
    EndpointApproval ParentApproval,
    EndpointApproval ChildApproval,
    AccountId ActorId) : LinkEvent(LinkId, ActorId);

/// <summary>A consent change on one endpoint (D-013). <see cref="Endpoint"/> is that endpoint's person.</summary>
public abstract record LinkEndpointEvent(LinkId LinkId, PersonId Endpoint, AccountId ActorId) : LinkEvent(LinkId, ActorId);

/// <summary>Pending → approved.</summary>
public sealed record LinkEndpointApproved(LinkId LinkId, PersonId Endpoint, AccountId ActorId)
    : LinkEndpointEvent(LinkId, Endpoint, ActorId);

/// <summary>Disagreed → approved.</summary>
public sealed record LinkApprovalRestored(LinkId LinkId, PersonId Endpoint, AccountId ActorId)
    : LinkEndpointEvent(LinkId, Endpoint, ActorId);

/// <summary>Pending → disagreed.</summary>
public sealed record LinkEndpointDeclined(LinkId LinkId, PersonId Endpoint, AccountId ActorId)
    : LinkEndpointEvent(LinkId, Endpoint, ActorId);

/// <summary>
/// Approved → disagreed: a claimed person's disagreement, or a maintainer withdrawing approval to
/// remove the link (PRD 03). Distinct from <see cref="LinkDeleted"/>.
/// </summary>
public sealed record LinkEndpointDisagreed(LinkId LinkId, PersonId Endpoint, AccountId ActorId)
    : LinkEndpointEvent(LinkId, Endpoint, ActorId);

/// <summary>Deleted by the account that maintains both endpoints (D-014).</summary>
public sealed record LinkDeleted(LinkId LinkId, DateTimeOffset DeletedAt, AccountId ActorId) : LinkEvent(LinkId, ActorId);

/// <summary>
/// The parent's maintainer set or cleared a child's birth order (D-016). It needs no consent, so it
/// is a profile event, not a relationship-consent event.
/// </summary>
public sealed record LinkBirthOrderChanged(LinkId LinkId, PersonId ParentId, int? BirthOrder, AccountId ActorId)
    : AuditEvent(ActorId)
{
    public override AuditFamily Family => AuditFamily.Profile;
}
