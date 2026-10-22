using FamilyGraph.Business.Audit;

namespace FamilyGraph.Business.People;

public abstract record PersonEvent(PersonId PersonId, AccountId ActorId) : AuditEvent(ActorId)
{
    public override AuditFamily Family => AuditFamily.Profile;
}

public sealed record PersonCreated(PersonId PersonId, AccountId ActorId) : PersonEvent(PersonId, ActorId);

/// <summary>Records that the name changed, without the old or new name (D-015).</summary>
public sealed record PersonRenamed(PersonId PersonId, AccountId ActorId) : PersonEvent(PersonId, ActorId);
