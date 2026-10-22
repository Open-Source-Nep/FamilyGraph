using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Accounts;

/// <summary>
/// Signs users in with an external provider and reads their account (D-008). The first sign-in
/// registers the account. An account never creates a <see cref="Person"/> (PRD 01).
/// </summary>
public sealed class AccountService(IAccountStore accounts, AdministratorEmails administrators)
{
    /// <summary>
    /// Finds or creates the account for the login, refreshes its email, and grants or removes
    /// <see cref="AccountRoles.Administrator"/> to match the configured list.
    /// </summary>
    public async Task<AccountView> SignInAsync(ExternalLogin login, CancellationToken cancellationToken)
    {
        var isAdministrator = administrators.Matches(login.Email, login.EmailVerified);
        var existing = await accounts.FindByLoginAsync(login.Provider, login.ProviderKey, cancellationToken);
        if (existing is null)
        {
            return await accounts.CreateAsync(login, isAdministrator, cancellationToken);
        }

        if (existing.Email == login.Email
            && existing.EmailVerified == login.EmailVerified
            && existing.IsAdministrator == isAdministrator)
        {
            return existing;
        }

        return await accounts.UpdateAsync(existing.Id, login.Email, login.EmailVerified, isAdministrator, cancellationToken);
    }

    public Task<AccountView?> GetAsync(AccountId id, CancellationToken cancellationToken) =>
        accounts.FindAsync(id, cancellationToken);
}
