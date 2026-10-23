using FamilyGraph.Application.Handovers;
using FamilyGraph.Business.Handovers;
using FamilyGraph.Business.People;
using FamilyGraph.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyGraph.Infrastructure.Handovers;

internal sealed class EfHandoverStore(FamilyGraphDbContext context) : IHandoverStore
{
    public Task<ProfileHandover?> FindOpenAsync(PersonId personId, CancellationToken cancellationToken) =>
        context.Set<ProfileHandover>()
            .SingleOrDefaultAsync(handover => handover.PersonId == personId && handover.State == HandoverState.Open, cancellationToken);

    public Task<ProfileHandover?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        context.Set<ProfileHandover>().SingleOrDefaultAsync(handover => handover.TokenHash == tokenHash, cancellationToken);

    public void Add(ProfileHandover handover) => context.Add(handover);
}
