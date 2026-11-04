using System.Collections.Concurrent;
using FamilyGraph.Application.Claims;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.Claims;
using FamilyGraph.Business.People;
using Server.IntegrationTests.People;

namespace Server.IntegrationTests.Claims;

/// <summary>Identity claims in memory, over the people of <paramref name="people"/>. Added claims are kept at once.</summary>
public sealed class InMemoryClaims(InMemoryPeople people) : IClaimStore, IClaimQueries
{
    private readonly ConcurrentDictionary<ClaimId, ProfileClaim> _claims = [];

    public IReadOnlyCollection<ProfileClaim> All => _claims.Values.ToList();

    public Task<ProfileClaim?> FindAsync(ClaimId id, CancellationToken cancellationToken) =>
        Task.FromResult(_claims.GetValueOrDefault(id));

    public Task<IReadOnlyList<ProfileClaim>> ListOpenForPersonAsync(PersonId personId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProfileClaim>>(
            [.. _claims.Values.Where(claim => claim.PersonId == personId && claim.IsOpen).OrderBy(claim => claim.SubmittedAt)]);

    public Task<bool> AccountHasClaimAsync(AccountId accountId, CancellationToken cancellationToken) =>
        Task.FromResult(
            people.People.Values.Any(person => person.ClaimedById == accountId && !person.IsDeleted)
            || _claims.Values.Any(claim => claim.ClaimantId == accountId && claim.IsOpen));

    public void Add(ProfileClaim claim) => _claims[claim.Id] = claim;

    public Task<ClaimRecords> ListToDecideAsync(AccountId maintainerId, int limit, CancellationToken cancellationToken)
    {
        var open = _claims.Values.Where(claim => claim.IsOpen).ToList();
        var rows = open
            .Select(claim => (Claim: claim, Person: people.People[claim.PersonId]))
            .Where(row => row.Person.IsMaintainedBy(maintainerId) && !row.Person.IsDeleted && !row.Person.IsClaimed
                && open.Count(other => other.PersonId == row.Claim.PersonId) == 1)
            .OrderBy(row => row.Claim.SubmittedAt)
            .ToList();
        return Task.FromResult(Records(rows, maintainerId, limit));
    }

    public Task<ClaimRecords> ListByClaimantAsync(AccountId claimantId, int limit, CancellationToken cancellationToken)
    {
        var rows = _claims.Values
            .Where(claim => claim.ClaimantId == claimantId)
            .Select(claim => (Claim: claim, Person: people.People[claim.PersonId]))
            .OrderByDescending(row => row.Claim.SubmittedAt)
            .ToList();
        return Task.FromResult(Records(rows, claimantId, limit));
    }

    private static ClaimRecords Records(List<(ProfileClaim Claim, Person Person)> rows, AccountId viewerId, int limit) =>
        new([.. rows.Take(limit).Select(row => ClaimRecord.From(row.Claim, PersonRecord.From(row.Person, viewerId)))], rows.Count > limit);
}
