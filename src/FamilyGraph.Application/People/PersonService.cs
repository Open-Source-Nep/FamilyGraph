using FamilyGraph.Application.Persistence;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.People;

namespace FamilyGraph.Application.People;

/// <summary>
/// Creates, renames, sets the gender, visibility and image of, deletes and reads person profiles (PRD 01,
/// PRD 05, #12, #18, #74, #80).
/// The creating account becomes the maintainer, and only the maintainer can rename or change the
/// visibility; <see cref="Person"/> enforces both. A profile is never matched or merged by name.
/// </summary>
public sealed class PersonService(
    IPersonStore people,
    IPersonQueries queries,
    IProfileImageStore images,
    IAuditLog audit,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    PeopleLimits limits)
{
    public async Task<PersonId> CreateAsync(
        PersonName name, Gender? gender, AccountId actorId, CancellationToken cancellationToken)
    {
        var person = Person.Create(name, actorId, gender);
        people.Add(person);
        await SaveAsync(person, cancellationToken);
        return person.Id;
    }

    public async Task RenameAsync(PersonId id, PersonName name, AccountId actorId, CancellationToken cancellationToken)
    {
        var person = await people.FindAsync(id, cancellationToken) ?? throw new PersonNotFoundException();
        person.Rename(name, actorId);
        await SaveAsync(person, cancellationToken);
    }

    /// <summary>Sets, changes or clears the person's gender (D-030). Only the maintainer may.</summary>
    public async Task SetGenderAsync(PersonId id, Gender? gender, AccountId actorId, CancellationToken cancellationToken)
    {
        var person = await people.FindAsync(id, cancellationToken) ?? throw new PersonNotFoundException();
        person.SetGender(gender, actorId);
        await SaveAsync(person, cancellationToken);
    }

    /// <summary>
    /// Makes the profile Public or Private (PRD 05). Its relationships and their consent are
    /// unchanged, so its active links stay traversable through the placeholder.
    /// </summary>
    public async Task SetVisibilityAsync(
        PersonId id, ProfileVisibility visibility, AccountId actorId, CancellationToken cancellationToken)
    {
        var person = await people.FindAsync(id, cancellationToken) ?? throw new PersonNotFoundException();
        person.SetVisibility(visibility, actorId);
        await SaveAsync(person, cancellationToken);
    }

    /// <summary>
    /// Deletes the profile for good: its name is erased and it remains a <c>Deleted</c>
    /// placeholder (PRD 05). Its maintainer may, or an administrator for moderation. Its links and
    /// their consent are unchanged.
    /// </summary>
    public async Task DeleteAsync(PersonId id, Viewer actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        var person = await people.FindAsync(id, cancellationToken) ?? throw new PersonNotFoundException();
        var imageId = person.ImageId;
        person.Delete(actor.AccountId, actor.IsAdministrator, time.GetUtcNow());
        await RemoveImageRowAsync(imageId, cancellationToken);
        await SaveAsync(person, cancellationToken);
    }

    /// <summary>
    /// Sets or replaces the profile's image (D-028). Only its maintainer may. The replaced image is
    /// removed in the same transaction, so old images are not kept.
    /// </summary>
    public async Task<ProfileImageId> SetImageAsync(
        PersonId id, byte[] content, AccountId actorId, CancellationToken cancellationToken)
    {
        var person = await people.FindAsync(id, cancellationToken) ?? throw new PersonNotFoundException();
        var previousId = person.ImageId;
        var image = person.SetImage(content, actorId);
        images.Add(image);
        await RemoveImageRowAsync(previousId, cancellationToken);
        await SaveAsync(person, cancellationToken);
        return image.Id;
    }

    /// <summary>Removes the profile's image. Only its maintainer may.</summary>
    public async Task RemoveImageAsync(PersonId id, AccountId actorId, CancellationToken cancellationToken)
    {
        var person = await people.FindAsync(id, cancellationToken) ?? throw new PersonNotFoundException();
        var imageId = person.ImageId;
        person.RemoveImage(actorId);
        await RemoveImageRowAsync(imageId, cancellationToken);
        await SaveAsync(person, cancellationToken);
    }

    /// <summary>
    /// The profile's image, for any signed-in viewer who sees the profile by name. A placeholder
    /// never shows one, so a hidden or deleted profile answers like one without an image (D-028).
    /// </summary>
    public async Task<ImageContent> GetImageAsync(PersonId id, Viewer viewer, CancellationToken cancellationToken)
    {
        var person = await GetAsync(id, viewer, cancellationToken);
        var imageId = person.ImageId ?? throw new ProfileImageNotFoundException();
        return await queries.FindImageAsync(imageId, cancellationToken) ?? throw new ProfileImageNotFoundException();
    }

    /// <summary>
    /// Any signed-in account may view a profile. A Private one is a <c>Private</c> placeholder to
    /// everyone but its maintainer and administrators (PRD 05).
    /// </summary>
    public async Task<PersonReference> GetAsync(PersonId id, Viewer viewer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);

        var person = await queries.FindAsync(id, viewer.AccountId, cancellationToken) ?? throw new PersonNotFoundException();
        return PrivacyProjection.Project(person, viewer);
    }

    public Task<ProfileList> ListMaintainedAsync(AccountId maintainerId, CancellationToken cancellationToken) =>
        queries.ListMaintainedByAsync(maintainerId, limits.MaintainedListLimit, cancellationToken);

    private async Task RemoveImageRowAsync(ProfileImageId? imageId, CancellationToken cancellationToken)
    {
        if (imageId is { } existingId && await images.FindAsync(existingId, cancellationToken) is { } image)
        {
            images.Remove(image);
        }
    }

    private async Task SaveAsync(Person person, CancellationToken cancellationToken)
    {
        audit.Add(person.TakeEvents(), time.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
