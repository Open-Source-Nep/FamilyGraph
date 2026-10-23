using FamilyGraph.Business.Audit;

namespace FamilyGraph.Business.People;

/// <summary>
/// A real-world individual in the graph (PRD 01). A person is not an account: it only records
/// which account currently maintains the profile, and there is always exactly one maintainer.
/// </summary>
public sealed class Person : AuditedEntity
{
    private Person(PersonId id, PersonName name, AccountId maintainerId)
    {
        Id = id;
        Name = name;
        MaintainerId = maintainerId;
    }

    /// <summary>For persistence, which sets the properties after construction.</summary>
    private Person()
    {
        Name = null!;
    }

    public PersonId Id { get; }

    public PersonName Name { get; private set; }

    public AccountId MaintainerId { get; private set; }

    /// <summary>New profiles start Public (PRD 05).</summary>
    public ProfileVisibility Visibility { get; private set; } = ProfileVisibility.Public;

    /// <summary>Creates a profile. The creating account becomes its maintainer.</summary>
    public static Person Create(PersonName name, AccountId creatorId)
    {
        ArgumentNullException.ThrowIfNull(name);

        var person = new Person(PersonId.New(), name, creatorId);
        person.Record(new PersonCreated(person.Id, creatorId));
        return person;
    }

    public bool IsMaintainedBy(AccountId accountId) => MaintainerId == accountId;

    /// <summary>Renames the person. Only the current maintainer may do this.</summary>
    public void Rename(PersonName newName, AccountId actorId)
    {
        ArgumentNullException.ThrowIfNull(newName);

        if (!IsMaintainedBy(actorId))
        {
            throw new NotProfileMaintainerException(Id);
        }

        Name = newName;
        Record(new PersonRenamed(Id, actorId));
    }

    /// <summary>
    /// Makes the profile Public or Private. Only the current maintainer may do this (PRD 05). It
    /// changes what others see of the profile and nothing about its relationships or their consent.
    /// </summary>
    public void SetVisibility(ProfileVisibility visibility, AccountId actorId)
    {
        ArgumentNullException.ThrowIfNull(visibility);

        if (!IsMaintainedBy(actorId))
        {
            throw new NotProfileMaintainerException(Id);
        }

        if (Visibility == visibility)
        {
            throw new ProfileVisibilityUnchangedException(Id, visibility);
        }

        Visibility = visibility;
        Record(new PersonVisibilityChanged(Id, actorId, visibility));
    }
}

public sealed class NotProfileMaintainerException(PersonId personId)
    : NotPermittedException("person.not_maintainer", $"Only the maintainer of profile {personId.Value} can change it.");

public sealed class ProfileVisibilityUnchangedException(PersonId personId, ProfileVisibility visibility)
    : ConflictException("person.visibility_unchanged", $"Profile {personId.Value} is already {visibility}.");
