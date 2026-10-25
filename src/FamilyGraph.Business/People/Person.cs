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

    /// <summary>One line that tells people with the same name apart, or <see langword="null"/> (D-037).</summary>
    public string? Description { get; private set; }

    /// <summary>The birth date as entered, or <see langword="null"/> if it is not known (D-037).</summary>
    public BirthDate? BirthDate { get; private set; }

    /// <summary>
    /// Whether people other than the maintainer and administrators may see <see cref="BirthDate"/>.
    /// Off by default (D-037).
    /// </summary>
    public bool BirthDateShown { get; private set; }

    /// <summary>
    /// Creates a profile. The creating account becomes its maintainer. Only the name is required;
    /// the gender, description and birth date are optional (PRD 00, D-030, D-037).
    /// </summary>
    public static Person Create(
        PersonName name,
        AccountId creatorId,
        Gender? gender = null,
        string? description = null,
        BirthDate? birthDate = null,
        bool birthDateShown = false)
    {
        ArgumentNullException.ThrowIfNull(name);

        var person = new Person(PersonId.New(), name, creatorId)
        {
            Gender = gender,
            Description = PersonDescription.Normalize(description),
            BirthDate = birthDate,
            BirthDateShown = birthDateShown,
        };
        person.Record(new PersonCreated(person.Id, creatorId));
        return person;
    }

    /// <summary>
    /// Restores a profile from an export file with its ID (D-033). A deleted profile keeps only the
    /// erased name and no gender, description or birth date. It records no event: the import's one
    /// <c>DataImported</c> covers it.
    /// </summary>
    public static Person Import(
        PersonId id,
        PersonName name,
        ProfileVisibility visibility,
        DateTimeOffset? deletedAt,
        AccountId maintainerId,
        Gender? gender,
        string? description = null,
        BirthDate? birthDate = null,
        bool birthDateShown = false)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(visibility);

        return deletedAt is null
            ? new Person(id, name, maintainerId)
            {
                Visibility = visibility,
                Gender = gender,
                Description = PersonDescription.Normalize(description),
                BirthDate = birthDate,
                BirthDateShown = birthDateShown,
            }
            : new Person(id, PersonName.Erased, maintainerId) { Visibility = visibility, DeletedAt = deletedAt };
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
    /// Sets, changes or clears (blank) the description (D-037). Only the current maintainer may do
    /// this. The event does not record the text, which is personal data (D-015).
    /// </summary>
    public void SetDescription(string? description, AccountId actorId)
    {
        EnsureNotDeleted();
        if (!IsMaintainedBy(actorId))
        {
            throw new NotProfileMaintainerException(Id);
        }

        Description = PersonDescription.Normalize(description);
        Record(new PersonDescriptionChanged(Id, actorId));
    }

    /// <summary>
    /// Sets, changes or clears the birth date, and whether others may see it (D-037). Only the
    /// current maintainer may do this. The event records the switch, a setting, but not the date (D-015).
    /// </summary>
    public void SetBirthDate(BirthDate? birthDate, bool shown, AccountId actorId)
    {
        EnsureNotDeleted();
        if (!IsMaintainedBy(actorId))
        {
            throw new NotProfileMaintainerException(Id);
        }

        BirthDate = birthDate;
        BirthDateShown = shown;
        Record(new PersonBirthDateChanged(Id, actorId, shown));
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
    /// Deletes the profile: its name, gender, description, birth date and image are erased for good and it remains as a <c>Deleted</c>
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
        Description = null;
        BirthDate = null;
        BirthDateShown = false;
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

    /// <summary>
    /// An administrator makes <paramref name="newMaintainerId"/> the maintainer, without that account
    /// accepting anything (D-035). It is the one exception to voluntary handover (PRD 01). Links and
    /// their consent are unchanged, and an open handover link becomes void because its creator no
    /// longer maintains the profile.
    /// </summary>
    public void Transfer(AccountId newMaintainerId, AccountId actorId, bool actorIsAdministrator)
    {
        if (!actorIsAdministrator)
        {
            throw new TransferNotAdministratorException();
        }

        EnsureNotDeleted();
        if (IsMaintainedBy(newMaintainerId))
        {
            throw new TransferToSameMaintainerException(Id);
        }

        var previousMaintainerId = MaintainerId;
        MaintainerId = newMaintainerId;
        Record(new PersonTransferred(Id, actorId, previousMaintainerId, newMaintainerId));
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

/// <summary>Only an administrator may transfer a profile without the receiver accepting (D-035).</summary>
public sealed class TransferNotAdministratorException()
    : NotPermittedException("transfer.not_administrator", "Only an administrator can transfer profiles.");

public sealed class TransferToSameMaintainerException(PersonId personId)
    : ConflictException("transfer.same_maintainer", $"That account already maintains profile {personId.Value}.");
