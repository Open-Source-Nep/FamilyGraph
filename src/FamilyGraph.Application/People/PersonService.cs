using FamilyGraph.Application.Persistence;
using FamilyGraph.Business.People;

namespace FamilyGraph.Application.People;

/// <summary>
/// Creates, renames and reads person profiles (PRD 01, #12). The creating account becomes the
/// maintainer, and only the maintainer can rename; <see cref="Person"/> enforces both. A profile
/// is never matched or merged by name.
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
    /// Any signed-in account may view a profile. Profiles are Public until privacy settings
    /// arrive (PRD 05, #17), which will hide a Private profile's name behind a placeholder.
    /// </summary>
    public async Task<PersonView> GetAsync(PersonId id, AccountId viewerId, CancellationToken cancellationToken) =>
        await queries.FindAsync(id, viewerId, cancellationToken) ?? throw new PersonNotFoundException();

    public Task<ProfileList> ListMaintainedAsync(AccountId maintainerId, CancellationToken cancellationToken) =>
        queries.ListMaintainedByAsync(maintainerId, limits.MaintainedListLimit, cancellationToken);

    private async Task SaveAsync(Person person, CancellationToken cancellationToken)
    {
        audit.Add(person.TakeEvents(), time.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
