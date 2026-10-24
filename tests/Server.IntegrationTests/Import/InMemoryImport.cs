using FamilyGraph.Application.Import;
using FamilyGraph.Business.People;
using Server.IntegrationTests.Accounts;
using Server.IntegrationTests.People;

namespace Server.IntegrationTests.Import;

/// <summary>The import's store over the in-memory accounts and people; the transaction does nothing.</summary>
public sealed class InMemoryImport(InMemoryAccountStore accounts, InMemoryPeople people) : IImportStore, IImportTransaction
{
    public Task<IImportTransaction> BeginAsync(CancellationToken cancellationToken) => Task.FromResult<IImportTransaction>(this);

    public Task<bool> HasPeopleAsync(CancellationToken cancellationToken) => Task.FromResult(people.People.Count > 0);

    public Task<IReadOnlyDictionary<string, AccountId>> FindAccountsByVerifiedEmailAsync(
        IReadOnlyCollection<string> emails, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, AccountId>(StringComparer.OrdinalIgnoreCase);
        foreach (var email in emails)
        {
            var matches = accounts.Accounts.Values
                .Where(account => string.Equals(account.Email.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matches is [{ EmailVerified: true } match])
            {
                result[email] = match.Id;
            }
        }

        return Task.FromResult<IReadOnlyDictionary<string, AccountId>>(result);
    }

    public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
