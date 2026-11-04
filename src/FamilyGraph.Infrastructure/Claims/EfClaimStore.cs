using FamilyGraph.Application.Claims;
using FamilyGraph.Business.Claims;
using FamilyGraph.Business.People;
using FamilyGraph.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyGraph.Infrastructure.Claims;

internal sealed class EfClaimStore(FamilyGraphDbContext context) : IClaimStore
{
    public Task<ProfileClaim?> FindAsync(ClaimId id, CancellationToken cancellationToken) =>
        context.Set<ProfileClaim>().SingleOrDefaultAsync(claim => claim.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ProfileClaim>> ListOpenForPersonAsync(PersonId personId, CancellationToken cancellationToken) =>
        await context.Set<ProfileClaim>()
            .Where(claim => claim.PersonId == personId && claim.State == ClaimState.Open)
            .OrderBy(claim => claim.SubmittedAt)
            .ToListAsync(cancellationToken);

    public Task<ProfileClaim?> FindLatestAsync(PersonId personId, AccountId claimantId, CancellationToken cancellationToken) =>
        context.Set<ProfileClaim>()
            .Where(claim => claim.PersonId == personId && claim.ClaimantId == claimantId)
            .OrderByDescending(claim => claim.SubmittedAt)
            .ThenByDescending(claim => claim.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> AccountHasClaimAsync(AccountId accountId, CancellationToken cancellationToken) =>
        await context.Set<Person>().AnyAsync(person => person.ClaimedById == accountId && person.DeletedAt == null, cancellationToken)
        || await context.Set<ProfileClaim>().AnyAsync(claim => claim.ClaimantId == accountId && claim.State == ClaimState.Open, cancellationToken);

    public async Task<IReadOnlyList<ProfileClaim>> ListNotesToEraseAsync(
        DateTimeOffset closedBefore, int limit, CancellationToken cancellationToken) =>
        await context.Set<ProfileClaim>()
            .Where(claim => claim.State != ClaimState.Open && claim.NotesErasedAt == null && claim.ClosedAt < closedBefore)
            .OrderBy(claim => claim.ClosedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public void Add(ProfileClaim claim) => context.Add(claim);
}
