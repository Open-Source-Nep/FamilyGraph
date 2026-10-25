using FamilyGraph.Application.People;
using FamilyGraph.Business.People;
using FamilyGraph.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyGraph.Infrastructure.People;

internal sealed class EfPersonStore(FamilyGraphDbContext context) : IPersonStore
{
    public Task<Person?> FindAsync(PersonId id, CancellationToken cancellationToken) =>
        context.Set<Person>().SingleOrDefaultAsync(person => person.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Person>> ListMaintainedByAsync(AccountId maintainerId, CancellationToken cancellationToken) =>
        await context.Set<Person>()
            .Where(person => person.MaintainerId == maintainerId && person.DeletedAt == null)
            .OrderBy(person => person.Id)
            .ToListAsync(cancellationToken);

    public void Add(Person person) => context.Add(person);
}
