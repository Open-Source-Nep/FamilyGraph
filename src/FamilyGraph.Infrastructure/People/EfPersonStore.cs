using FamilyGraph.Application.People;
using FamilyGraph.Business.People;
using FamilyGraph.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyGraph.Infrastructure.People;

internal sealed class EfPersonStore(FamilyGraphDbContext context) : IPersonStore
{
    public Task<Person?> FindAsync(PersonId id, CancellationToken cancellationToken) =>
        context.Set<Person>().SingleOrDefaultAsync(person => person.Id == id, cancellationToken);

    public void Add(Person person) => context.Add(person);
}
