using FamilyGraph.Application.Accounts;
using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Tests.Accounts;

internal sealed class InMemoryAccountStore : IAccountStore
{
    private readonly Dictionary<(string Provider, string Key), AccountId> _logins = [];

    public Dictionary<AccountId, AccountView> Accounts { get; } = [];

    public Task<AccountView?> FindAsync(AccountId id, CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.GetValueOrDefault(id));

    public Task<AccountView?> FindByLoginAsync(string provider, string providerKey, CancellationToken cancellationToken) =>
        Task.FromResult(_logins.TryGetValue((provider, providerKey), out var id) ? Accounts[id] : null);

    public Task<AccountView> CreateAsync(ExternalLogin login, bool isAdministrator, CancellationToken cancellationToken)
    {
        var account = new AccountView(new AccountId(Guid.NewGuid()), login.Email, login.EmailVerified, isAdministrator);
        Accounts[account.Id] = account;
        _logins[(login.Provider, login.ProviderKey)] = account.Id;
        return Task.FromResult(account);
    }

    public Task<AccountView> UpdateAsync(
        AccountId id, string email, bool emailVerified, bool isAdministrator, CancellationToken cancellationToken)
    {
        var account = Accounts[id] with { Email = email, EmailVerified = emailVerified, IsAdministrator = isAdministrator };
        Accounts[id] = account;
        return Task.FromResult(account);
    }
}
