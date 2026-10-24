using FamilyGraph.Application.Relationships;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;
using FamilyGraph.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyGraph.Infrastructure.Relationships;

internal sealed class EfMarriageStore(FamilyGraphDbContext context) : IMarriageStore
{
    public Task<Marriage?> FindAsync(LinkId id, CancellationToken cancellationToken) =>
        context.Set<Marriage>().SingleOrDefaultAsync(marriage => marriage.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(PersonId spouseId, PersonId otherSpouseId, CancellationToken cancellationToken) =>
        context.Set<Marriage>().AnyAsync(
            marriage => marriage.DeletedAt == null
                && ((marriage.FirstSpouseId == spouseId && marriage.SecondSpouseId == otherSpouseId)
                    || (marriage.FirstSpouseId == otherSpouseId && marriage.SecondSpouseId == spouseId)),
            cancellationToken);

    public Task<bool> AreParentAndChildAsync(PersonId personId, PersonId otherPersonId, CancellationToken cancellationToken) =>
        context.Set<ParentChildLink>().AnyAsync(
            link => link.DeletedAt == null
                && ((link.ParentId == personId && link.ChildId == otherPersonId)
                    || (link.ParentId == otherPersonId && link.ChildId == personId)),
            cancellationToken);

    public void Add(Marriage marriage) => context.Add(marriage);
}
