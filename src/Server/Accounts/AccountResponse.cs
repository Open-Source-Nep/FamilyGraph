using FamilyGraph.Application.Accounts;

namespace Server.Accounts;

/// <summary>The signed-in user's own account (D-008: view own account).</summary>
public sealed record AccountResponse(Guid Id, string Email, bool EmailVerified, IReadOnlyList<string> Roles)
{
    internal static AccountResponse From(AccountView account) =>
        new(account.Id.Value, account.Email, account.EmailVerified, account.Roles);
}
