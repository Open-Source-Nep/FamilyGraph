using FamilyGraph.Application.Accounts;
using Server.IntegrationTests.Accounts;
using Server.IntegrationTests.People;

namespace Server.IntegrationTests.Administration;

/// <summary>The account list over the in-memory account store and people.</summary>
public sealed class InMemoryAccountQueries(InMemoryAccountStore accounts, InMemoryPeople people) : IAccountQueries
{
    public Task<AccountList> ListAsync(int limit, CancellationToken cancellationToken)
    {
        var all = accounts.Accounts.Values
            .OrderBy(account => account.Email, StringComparer.Ordinal)
            .ThenBy(account => account.Id.Value)
            .Select(account => new AccountSummary(
                account.Id,
                account.Email,
                account.EmailVerified,
                account.IsAdministrator,
                people.People.Values.Count(person => person.IsMaintainedBy(account.Id) && !person.IsDeleted)))
            .ToList();
        return Task.FromResult(new AccountList(all.Take(limit).ToList(), all.Count > limit));
    }
}
