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

        var rows = await OpenClaims().Where(row => row.Claim.ReviewRequestedAt == null && row.Person.MaintainerId == maintainerId
                    && row.Person.DeletedAt == null && row.Person.ClaimedById == null && row.OpenClaims == 1)
            .OrderBy(row => row.Claim.SubmittedAt).ThenBy(row => row.Claim.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        return Records(rows, maintainerId, limit);
    }

    public async Task<ClaimRecords> ListToRespondAsync(AccountId maintainerId, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        var rows = await OpenClaims().Where(row => row.Person.MaintainerId == maintainerId && row.Person.DeletedAt == null && row.Person.ClaimedById != null)
            .OrderBy(row => row.Claim.SubmittedAt).ThenBy(row => row.Claim.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        return Records(rows, maintainerId, limit);
    }

    public async Task<ClaimRecords> ListForAdministratorsAsync(AccountId viewerId, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        var rows = await OpenClaims().Where(row => row.Person.DeletedAt == null
                    && (row.Claim.ReviewRequestedAt != null || row.Person.ClaimedById != null || row.OpenClaims > 1))
            .OrderBy(row => row.Claim.ReviewRequestedAt ?? row.Claim.SubmittedAt).ThenBy(row => row.Claim.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        return Records(rows, viewerId, limit);
    }

    public async Task<ClaimRecords> ListByClaimantAsync(AccountId claimantId, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        var rows = await AllClaims().Where(row => row.Claim.ClaimantId == claimantId)
            .OrderByDescending(row => row.Claim.SubmittedAt).ThenBy(row => row.Claim.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        return Records(rows, claimantId, limit);
    }

    private IQueryable<Row> AllClaims() =>
        from claim in context.Set<ProfileClaim>().AsNoTracking()
        join person in context.Set<Person>().AsNoTracking() on claim.PersonId equals person.Id
        select new Row
        {
            Claim = claim,
            Person = person,
            OpenClaims = context.Set<ProfileClaim>().Count(other => other.PersonId == claim.PersonId && other.State == ClaimState.Open),
        };

    private IQueryable<Row> OpenClaims() => AllClaims().Where(row => row.Claim.State == ClaimState.Open);

    private static ClaimRecords Records(List<Row> rows, AccountId viewerId, int limit) =>
        new(
            [.. rows.Take(limit).Select(row => ClaimRecord.From(row.Claim, row.Person, viewerId, row.OpenClaims))],
            rows.Count > limit);

    private sealed class Row
    {
        public required ProfileClaim Claim { get; init; }

        public required Person Person { get; init; }

        public required int OpenClaims { get; init; }
    }
}
