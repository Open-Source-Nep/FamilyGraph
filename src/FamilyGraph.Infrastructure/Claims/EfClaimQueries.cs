using FamilyGraph.Application.Claims;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.Claims;
using FamilyGraph.Business.People;
using FamilyGraph.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyGraph.Infrastructure.Claims;

internal sealed class EfClaimQueries(FamilyGraphDbContext context) : IClaimQueries
{
    public async Task<ClaimRecords> ListToDecideAsync(AccountId maintainerId, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        var open = context.Set<ProfileClaim>().AsNoTracking().Where(claim => claim.State == ClaimState.Open);
        var rows = await (
                from claim in open
                join person in context.Set<Person>().AsNoTracking() on claim.PersonId equals person.Id
                where person.MaintainerId == maintainerId && person.DeletedAt == null && person.ClaimedById == null
                    && open.Count(other => other.PersonId == claim.PersonId) == 1
                orderby claim.SubmittedAt, claim.Id
                select new { Claim = claim, Person = person })
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        return Records(rows.Select(row => (row.Claim, row.Person)).ToList(), maintainerId, limit);
    }

    public async Task<ClaimRecords> ListByClaimantAsync(AccountId claimantId, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        var rows = await (
                from claim in context.Set<ProfileClaim>().AsNoTracking()
                where claim.ClaimantId == claimantId
                join person in context.Set<Person>().AsNoTracking() on claim.PersonId equals person.Id
                orderby claim.SubmittedAt descending, claim.Id
                select new { Claim = claim, Person = person })
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        return Records(rows.Select(row => (row.Claim, row.Person)).ToList(), claimantId, limit);
    }

    private static ClaimRecords Records(List<(ProfileClaim Claim, Person Person)> rows, AccountId viewerId, int limit) =>
        new(
            [.. rows.Take(limit).Select(row => ClaimRecord.From(row.Claim, PersonRecord.From(row.Person, viewerId)))],
            rows.Count > limit);
}
