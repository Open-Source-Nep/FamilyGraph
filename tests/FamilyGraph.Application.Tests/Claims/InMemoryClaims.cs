using System.Collections.Concurrent;
using FamilyGraph.Application.Claims;
using FamilyGraph.Business.Claims;
using FamilyGraph.Business.People;
using FamilyGraph.Application.Tests.People;

namespace FamilyGraph.Application.Tests.Claims;

/// <summary>Identity claims in memory, over the people of <paramref name="people"/>. Added claims are kept at once.</summary>
internal sealed class InMemoryClaims(InMemoryPeople people) : IClaimStore, IClaimQueries
{
    private readonly ConcurrentDictionary<ClaimId, ProfileClaim> _claims = [];

    public IReadOnlyCollection<ProfileClaim> All => _claims.Values.ToList();

    public Task<ProfileClaim?> FindAsync(ClaimId id, CancellationToken cancellationToken) =>
        Task.FromResult(_claims.GetValueOrDefault(id));

    public Task<IReadOnlyList<ProfileClaim>> ListOpenForPersonAsync(PersonId personId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProfileClaim>>(
            [.. _claims.Values.Where(claim => claim.PersonId == personId && claim.IsOpen).OrderBy(claim => claim.SubmittedAt)]);

    public Task<ProfileClaim?> FindLatestAsync(PersonId personId, AccountId claimantId, CancellationToken cancellationToken) =>
        Task.FromResult(_claims.Values
            .Where(claim => claim.PersonId == personId && claim.ClaimantId == claimantId)
            .OrderByDescending(claim => claim.SubmittedAt)
            .FirstOrDefault());

    public Task<bool> AccountHasClaimAsync(AccountId accountId, CancellationToken cancellationToken) =>
        Task.FromResult(
            people.People.Values.Any(person => person.ClaimedById == accountId && !person.IsDeleted)
            || _claims.Values.Any(claim => claim.ClaimantId == accountId && claim.IsOpen));

    public Task<IReadOnlyList<ProfileClaim>> ListNotesToEraseAsync(DateTimeOffset closedBefore, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProfileClaim>>(
            [.. _claims.Values.Where(claim => !claim.IsOpen && claim.NotesErasedAt is null && claim.ClosedAt < closedBefore).Take(limit)]);

    public void Add(ProfileClaim claim) => _claims[claim.Id] = claim;

    public Task<ClaimRecords> ListToDecideAsync(AccountId maintainerId, int limit, CancellationToken cancellationToken) =>
        Task.FromResult(Records(
            Open().Where(row => row.Person.IsMaintainedBy(maintainerId) && !row.Person.IsDeleted && row.Claim.IsForMaintainer(row.Person, row.Open)),
            maintainerId,
            limit));

    public Task<ClaimRecords> ListToRespondAsync(AccountId maintainerId, int limit, CancellationToken cancellationToken) =>
        Task.FromResult(Records(
            Open().Where(row => row.Person.IsMaintainedBy(maintainerId) && !row.Person.IsDeleted && row.Person.IsClaimed),
            maintainerId,
            limit));

    public Task<ClaimRecords> ListForAdministratorsAsync(AccountId viewerId, int limit, CancellationToken cancellationToken) =>
        Task.FromResult(Records(
            Open().Where(row => !row.Person.IsDeleted && row.Claim.IsForAdministrators(row.Person.IsClaimed, row.Open)),
            viewerId,
            limit));

    public Task<ClaimRecords> ListByClaimantAsync(AccountId claimantId, int limit, CancellationToken cancellationToken) =>
        Task.FromResult(Records(
            Rows().Where(row => row.Claim.ClaimantId == claimantId).OrderByDescending(row => row.Claim.SubmittedAt),
            claimantId,
            limit));

    private IEnumerable<(ProfileClaim Claim, Person Person, int Open)> Rows() =>
        _claims.Values.Select(claim => (claim, people.People[claim.PersonId], _claims.Values.Count(other => other.PersonId == claim.PersonId && other.IsOpen)));

    private IEnumerable<(ProfileClaim Claim, Person Person, int Open)> Open() =>
        Rows().Where(row => row.Claim.IsOpen).OrderBy(row => row.Claim.SubmittedAt);

    private static ClaimRecords Records(IEnumerable<(ProfileClaim Claim, Person Person, int Open)> rows, AccountId viewerId, int limit)
    {
        var list = rows.ToList();
        return new([.. list.Take(limit).Select(row => ClaimRecord.From(row.Claim, row.Person, viewerId, row.Open))], list.Count > limit);
    }
}
