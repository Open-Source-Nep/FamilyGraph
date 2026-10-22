using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Accounts;

/// <summary>Persists user accounts. Infrastructure implements it with ASP.NET Core Identity (D-008).</summary>
public interface IAccountStore
{
    Task<AccountView?> FindAsync(AccountId id, CancellationToken cancellationToken);

    Task<AccountView?> FindByLoginAsync(string provider, string providerKey, CancellationToken cancellationToken);

    /// <summary>Creates an account linked to the external login, with the <see cref="AccountRoles.User"/> role.</summary>
    Task<AccountView> CreateAsync(ExternalLogin login, bool isAdministrator, CancellationToken cancellationToken);

    Task<AccountView> UpdateAsync(AccountId id, string email, bool emailVerified, bool isAdministrator, CancellationToken cancellationToken);
}
