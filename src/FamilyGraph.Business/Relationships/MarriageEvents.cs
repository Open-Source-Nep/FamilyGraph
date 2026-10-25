using FamilyGraph.Business.Audit;
using FamilyGraph.Business.People;

namespace FamilyGraph.Business.Relationships;

/// <summary>A relationship-consent event about one marriage (PRD 07, D-015, D-031).</summary>
public abstract record MarriageEvent(LinkId MarriageId, AccountId ActorId) : AuditEvent(ActorId)
{
    public override AuditFamily Family => AuditFamily.RelationshipConsent;
}

public sealed record MarriageCreated(
    LinkId MarriageId,
    PersonId FirstSpouseId,
    PersonId SecondSpouseId,
    EndpointApproval FirstApproval,
    EndpointApproval SecondApproval,
    AccountId ActorId) : MarriageEvent(MarriageId, ActorId);

/// <summary>A consent change on one spouse's side. <see cref="Spouse"/> is that side's person.</summary>
public abstract record MarriageSpouseEvent(LinkId MarriageId, PersonId Spouse, AccountId ActorId)
    : MarriageEvent(MarriageId, ActorId);

/// <summary>Pending → approved.</summary>
public sealed record MarriageSpouseApproved(LinkId MarriageId, PersonId Spouse, AccountId ActorId)
    : MarriageSpouseEvent(MarriageId, Spouse, ActorId);

/// <summary>
/// Pending → approved by an administrator on behalf of <see cref="MaintainerId"/>, that spouse's
/// maintainer at the time (D-040). Distinct from <see cref="MarriageSpouseApproved"/>.
/// </summary>
public sealed record MarriageSpouseApprovedByAdministrator(LinkId MarriageId, PersonId Spouse, AccountId ActorId, AccountId MaintainerId)
    : MarriageSpouseEvent(MarriageId, Spouse, ActorId);

/// <summary>Disagreed → approved.</summary>
public sealed record MarriageApprovalRestored(LinkId MarriageId, PersonId Spouse, AccountId ActorId)
    : MarriageSpouseEvent(MarriageId, Spouse, ActorId);

/// <summary>Pending → disagreed.</summary>
public sealed record MarriageSpouseDeclined(LinkId MarriageId, PersonId Spouse, AccountId ActorId)
    : MarriageSpouseEvent(MarriageId, Spouse, ActorId);

/// <summary>Approved → disagreed: a withdrawn approval, distinct from <see cref="MarriageDeleted"/> (PRD 03).</summary>
public sealed record MarriageSpouseDisagreed(LinkId MarriageId, PersonId Spouse, AccountId ActorId)
    : MarriageSpouseEvent(MarriageId, Spouse, ActorId);

/// <summary>Deleted by the account that maintains both spouses.</summary>
public sealed record MarriageDeleted(LinkId MarriageId, DateTimeOffset DeletedAt, AccountId ActorId)
    : MarriageEvent(MarriageId, ActorId);

/// <summary>
/// A spouse's maintainer marked the marriage former or current (D-031). It needs no consent, so,
/// like a birth-order change, it is a profile event, not a relationship-consent event.
/// </summary>
public sealed record MarriageFormerChanged(LinkId MarriageId, bool IsFormer, AccountId ActorId) : AuditEvent(ActorId)
{
    public override AuditFamily Family => AuditFamily.Profile;
}
