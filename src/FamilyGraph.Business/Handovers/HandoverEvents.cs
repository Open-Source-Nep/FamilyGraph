using FamilyGraph.Business.Audit;
using FamilyGraph.Business.People;

namespace FamilyGraph.Business.Handovers;

public abstract record HandoverEvent(HandoverId HandoverId, PersonId PersonId, AccountId ActorId) : AuditEvent(ActorId)
{
    public override AuditFamily Family => AuditFamily.Profile;
}

/// <summary>The maintainer created a handover link; it never holds the link's secret (D-015).</summary>
public sealed record HandoverOffered(HandoverId HandoverId, PersonId PersonId, AccountId ActorId, DateTimeOffset ExpiresAt)
    : HandoverEvent(HandoverId, PersonId, ActorId);

/// <summary>The maintainer cancelled an open handover link, or replaced it with a new one.</summary>
public sealed record HandoverCancelled(HandoverId HandoverId, PersonId PersonId, AccountId ActorId)
    : HandoverEvent(HandoverId, PersonId, ActorId);
