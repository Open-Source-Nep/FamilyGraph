using FamilyGraph.Application.Accounts;
using FamilyGraph.Application.Import;
using FamilyGraph.Business.People;
using Server.IntegrationTests.Accounts;
using Server.IntegrationTests.People;

namespace Server.IntegrationTests.Import;

/// <summary>The import's store over the in-memory accounts and people; the transaction does nothing.</summary>
public sealed class InMemoryImport(InMemoryAccountStore accounts, InMemoryPeople people) : IImportStore, IImportTransaction
{
    /// <summary>The audit rows restored as they were.</summary>
    public List<ImportedAuditEvent> RestoredAuditEvents { get; } = [];

    public Task<IImportTransaction> BeginAsync(CancellationToken cancellationToken) => Task.FromResult<IImportTransaction>(this);

    public Task<bool> HasPeopleAsync(CancellationToken cancellationToken) => Task.FromResult(people.People.Count > 0);

    public Task<IReadOnlyDictionary<string, AccountId>> FindAccountsByGoogleIdAsync(
        IReadOnlyCollection<string> googleIds, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, AccountId>();
        foreach (var googleId in googleIds)
        {
            if (accounts.FindByGoogleId(googleId) is { } id)
            {
                result[googleId] = id;
            }
        }

        return Task.FromResult<IReadOnlyDictionary<string, AccountId>>(result);
    }

    public Task<IReadOnlySet<AccountId>> FindExistingAccountsAsync(
        IReadOnlyCollection<AccountId> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<AccountId>>(ids.Where(accounts.Accounts.ContainsKey).ToHashSet());

    public Task CreateAccountAsync(ImportedAccount account, CancellationToken cancellationToken)
    {
        accounts.Add(new AccountView(account.Id, account.Email, account.EmailVerified, IsAdministrator: false), account.GoogleId);
        return Task.CompletedTask;
    }

    public Task AddImageAsync(ProfileImage image, CancellationToken cancellationToken)
    {
        people.Images[image.Id] = image;
        return Task.CompletedTask;
    }

    public Task AddAuditEventsAsync(IReadOnlyList<ImportedAuditEvent> auditEvents, CancellationToken cancellationToken)
    {
        RestoredAuditEvents.AddRange(auditEvents);
        return Task.CompletedTask;
    }

    public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
