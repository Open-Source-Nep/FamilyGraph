using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Accounts;

/// <summary>A user account as Application sees it. It is never a <see cref="Person"/> (PRD 01).</summary>
public sealed record AccountView(AccountId Id, string Email, bool EmailVerified, bool IsAdministrator)
{
    public IReadOnlyList<string> Roles => IsAdministrator ? [AccountRoles.User, AccountRoles.Administrator] : [AccountRoles.User];
}
