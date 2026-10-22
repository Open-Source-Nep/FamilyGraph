using FamilyGraph.Application.Accounts;
using FamilyGraph.Business.People;
using FamilyGraph.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FamilyGraph.Infrastructure.Identity;

internal sealed class IdentityAccountStore(UserManager<UserAccount> users, FamilyGraphDbContext context) : IAccountStore
{
    public async Task<AccountView?> FindAsync(AccountId id, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(id.Value.ToString());
        return user is null ? null : await ToViewAsync(user);
    }

    public async Task<AccountView?> FindByLoginAsync(string provider, string providerKey, CancellationToken cancellationToken)
    {
        var user = await users.FindByLoginAsync(provider, providerKey);
        return user is null ? null : await ToViewAsync(user);
    }

    public async Task<AccountView> CreateAsync(ExternalLogin login, bool isAdministrator, CancellationToken cancellationToken)
    {
        var id = Guid.CreateVersion7();
        var user = new UserAccount
        {
            Id = id,
            UserName = id.ToString("N"),
            Email = login.Email,
            EmailConfirmed = login.EmailVerified,
        };

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        Check(await users.CreateAsync(user));
        Check(await users.AddLoginAsync(user, new UserLoginInfo(login.Provider, login.ProviderKey, login.Provider)));
        Check(await users.AddToRolesAsync(user, isAdministrator
            ? [AccountRoles.User, AccountRoles.Administrator]
            : [AccountRoles.User]));
        await transaction.CommitAsync(cancellationToken);

        return new AccountView(new AccountId(id), login.Email, login.EmailVerified, isAdministrator);
    }

    public async Task<AccountView> UpdateAsync(
        AccountId id, string email, bool emailVerified, bool isAdministrator, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(id.Value.ToString())
            ?? throw new InvalidOperationException($"Account {id.Value} does not exist.");

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        user.Email = email;
        user.EmailConfirmed = emailVerified;
        Check(await users.UpdateAsync(user));

        var wasAdministrator = await users.IsInRoleAsync(user, AccountRoles.Administrator);
        if (isAdministrator && !wasAdministrator)
        {
            Check(await users.AddToRoleAsync(user, AccountRoles.Administrator));
        }
        else if (!isAdministrator && wasAdministrator)
        {
            Check(await users.RemoveFromRoleAsync(user, AccountRoles.Administrator));
        }

        await transaction.CommitAsync(cancellationToken);
        return new AccountView(id, email, emailVerified, isAdministrator);
    }

    private async Task<AccountView> ToViewAsync(UserAccount user) => new(
        new AccountId(user.Id),
        user.Email ?? "",
        user.EmailConfirmed,
        await users.IsInRoleAsync(user, AccountRoles.Administrator));

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Account update failed: " + string.Join("; ", result.Errors.Select(error => error.Code)));
        }
    }
}
