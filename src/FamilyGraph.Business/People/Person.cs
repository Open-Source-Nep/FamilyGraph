using FamilyGraph.Business.Audit;
using FamilyGraph.Business.Handovers;

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

    /// <summary>When the profile was deleted and its name erased, or <see langword="null"/>.</summary>
    public DateTimeOffset? DeletedAt { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    /// <summary>The profile's current <see cref="ProfileImage"/>, or <see langword="null"/> if it has none.</summary>
    public ProfileImageId? ImageId { get; private set; }

    /// <summary>The person's gender, or <see langword="null"/> if it is not set (D-030).</summary>
    public Gender? Gender { get; private set; }

    /// <summary>
    /// Creates a profile. The creating account becomes its maintainer. Only the name is required;
    /// the gender is optional (PRD 00, D-030).
    /// </summary>
    public static Person Create(PersonName name, AccountId creatorId, Gender? gender = null)
    {
        ArgumentNullException.ThrowIfNull(name);

        var person = new Person(PersonId.New(), name, creatorId) { Gender = gender };
        person.Record(new PersonCreated(person.Id, creatorId));
        return person;
    }

    public bool IsMaintainedBy(AccountId accountId) => MaintainerId == accountId;

    /// <summary>Renames the person. Only the current maintainer may do this.</summary>
    public void Rename(PersonName newName, AccountId actorId)
    {
        ArgumentNullException.ThrowIfNull(newName);

        EnsureNotDeleted();
        if (!IsMaintainedBy(actorId))
        {
            throw new NotProfileMaintainerException(Id);
        }

        Name = newName;
        Record(new PersonRenamed(Id, actorId));
    }

    /// <summary>
    /// Sets, changes or clears the person's gender (D-030). Only the current maintainer may do this.
    /// The event does not record the gender, which is personal data (D-015).
    /// </summary>
    public void SetGender(Gender? gender, AccountId actorId)
    {
        EnsureNotDeleted();
        if (!IsMaintainedBy(actorId))
        {
            throw new NotProfileMaintainerException(Id);
        }

        Gender = gender;
        Record(new PersonGenderChanged(Id, actorId));
    }

    /// <summary>
    /// Makes the profile Public or Private. Only the current maintainer may do this (PRD 05). It
    /// changes what others see of the profile and nothing about its relationships or their consent.
    /// </summary>
    public void SetVisibility(ProfileVisibility visibility, AccountId actorId)
    {
        ArgumentNullException.ThrowIfNull(visibility);

        EnsureNotDeleted();
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

    /// <summary>
    /// Deletes the profile: its name, gender and image are erased for good and it remains as a <c>Deleted</c>
    /// placeholder (PRD 05). Its maintainer may do this, or an administrator for moderation; the
    /// event records which. Its links and their consent are unchanged, so active links stay
    /// traversable.
    /// </summary>
    public void Delete(AccountId actorId, bool actorIsAdministrator, DateTimeOffset deletedAt)
    {
        EnsureNotDeleted();
        var deleter = IsMaintainedBy(actorId) ? ProfileDeleter.Maintainer
            : actorIsAdministrator ? ProfileDeleter.Administrator
            : throw new NotProfileMaintainerException(Id);

        Name = PersonName.Erased;
        Gender = null;
        ImageId = null;
        DeletedAt = deletedAt;
        Record(new PersonDeleted(Id, actorId, deleter));
    }

    /// <summary>
    /// Sets or replaces the profile's image (D-028). Only the current maintainer may do this. The
    /// caller stores the returned image and removes the one it replaces.
    /// </summary>
    public ProfileImage SetImage(byte[] content, AccountId actorId)
    {
        EnsureNotDeleted();
        if (!IsMaintainedBy(actorId))
        {
            throw new NotProfileMaintainerException(Id);
        }

        var image = ProfileImage.Create(Id, content);
        ImageId = image.Id;
        Record(new PersonImageChanged(Id, actorId));
        return image;
    }

    /// <summary>Removes the profile's image. Only the current maintainer may do this.</summary>
    public void RemoveImage(AccountId actorId)
    {
        EnsureNotDeleted();
        if (!IsMaintainedBy(actorId))
        {
            throw new NotProfileMaintainerException(Id);
        }

        if (ImageId is null)
        {
            throw new ProfileHasNoImageException(Id);
        }

        ImageId = null;
        Record(new PersonImageRemoved(Id, actorId));
    }

    /// <summary>
    /// Makes <paramref name="actorId"/> the maintainer through a handover link its current
    /// maintainer created (PRD 01). The previous maintainer keeps no authority over the profile.
    /// Links and their consent are unchanged: approvals already given stay as they are.
    /// </summary>
    public void AcceptHandover(ProfileHandover handover, AccountId actorId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(handover);

        EnsureNotDeleted();
        handover.EnsureCanBeAccepted(this, now);
        if (IsMaintainedBy(actorId))
        {
            throw new HandoverOwnProfileException();
        }

        var previousMaintainerId = MaintainerId;
        MaintainerId = actorId;
        handover.MarkAccepted(actorId, now);
        Record(new PersonHandedOver(Id, actorId, previousMaintainerId, handover.Id));
    }

    /// <summary>A deleted profile accepts no further change, nor consent on its side of a link.</summary>
    public void EnsureNotDeleted()
    {
        if (IsDeleted)
        {
            throw new ProfileDeletedException(Id);
        }
    }
}

public sealed class NotProfileMaintainerException(PersonId personId)
    : NotPermittedException("person.not_maintainer", $"Only the maintainer of profile {personId.Value} can change it.");

public sealed class ProfileDeletedException(PersonId personId)
    : ConflictException("person.deleted", $"Profile {personId.Value} was deleted.");

public sealed class ProfileHasNoImageException(PersonId personId)
    : ConflictException("person.no_image", $"Profile {personId.Value} has no image.");

public sealed class ProfileVisibilityUnchangedException(PersonId personId, ProfileVisibility visibility)
    : ConflictException("person.visibility_unchanged", $"Profile {personId.Value} is already {visibility}.");
