using FamilyGraph.Application.Persistence;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.People;

namespace FamilyGraph.Application.People;

/// <summary>
/// Creates, renames, sets the visibility of and reads person profiles (PRD 01, PRD 05, #12, #18).
/// The creating account becomes the maintainer, and only the maintainer can rename or change the
/// visibility; <see cref="Person"/> enforces both. A profile is never matched or merged by name.
/// </summary>
public sealed class PersonService(
    IPersonStore people,
    IPersonQueries queries,
    IAuditLog audit,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    PeopleLimits limits)
{
    public async Task<PersonId> CreateAsync(PersonName name, AccountId actorId, CancellationToken cancellationToken)
    {
        var person = Person.Create(name, actorId);
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

    private async Task SaveAsync(Person person, CancellationToken cancellationToken)
    {
        audit.Add(person.TakeEvents(), time.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
