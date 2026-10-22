using FamilyGraph.Application.People;
using FamilyGraph.Business.People;
using FamilyGraph.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyGraph.Infrastructure.People;

internal sealed class EfPersonQueries(FamilyGraphDbContext context) : IPersonQueries
{
    public async Task<PersonView?> FindAsync(PersonId id, AccountId viewerId, CancellationToken cancellationToken)
    {
        var person = await context.Set<Person>()
            .AsNoTracking()
            .Where(person => person.Id == id)
            .Select(person => new { person.Name, person.MaintainerId })
            .SingleOrDefaultAsync(cancellationToken);

        return person is null ? null : new PersonView(id, person.Name, person.MaintainerId == viewerId);
    }

    public async Task<ProfileList> ListMaintainedByAsync(AccountId maintainerId, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        // One extra row tells whether the list was cut short.
        var people = await context.Set<Person>()
            .AsNoTracking()
            .Where(person => person.MaintainerId == maintainerId)
            .OrderBy(person => person.Name.FirstName)
            .ThenBy(person => person.Name.MiddleName)
            .ThenBy(person => person.Name.LastName)
            .ThenBy(person => person.Id)
            .Select(person => new PersonSummary(person.Id, person.Name))
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        return people.Count > limit
            ? new ProfileList(people[..limit], IsTruncated: true)
            : new ProfileList(people, IsTruncated: false);
    }
}
