using FamilyGraph.Business.Audit;

namespace FamilyGraph.Business.People;

public abstract record PersonEvent(PersonId PersonId, AccountId ActorId) : AuditEvent(ActorId)
{
    public override AuditFamily Family => AuditFamily.Profile;
}

public sealed record PersonCreated(PersonId PersonId, AccountId ActorId) : PersonEvent(PersonId, ActorId);

/// <summary>Records that the name changed, without the old or new name (D-015).</summary>
public sealed record PersonRenamed(PersonId PersonId, AccountId ActorId) : PersonEvent(PersonId, ActorId);

/// <summary>Records the new visibility, which is a setting and not personal data (D-015).</summary>
public sealed record PersonVisibilityChanged(PersonId PersonId, AccountId ActorId, ProfileVisibility Visibility)
    : PersonEvent(PersonId, ActorId);

/// <summary>Records who deleted the profile and in which role, never the erased name (AC-05.11, D-015).</summary>
public sealed record PersonDeleted(PersonId PersonId, AccountId ActorId, ProfileDeleter DeletedBy)
    : PersonEvent(PersonId, ActorId);
