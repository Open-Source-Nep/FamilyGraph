namespace FamilyGraph.Application.Accounts;

/// <summary>
/// The two application roles (D-008). Every account has <see cref="User"/>. "Maintainer" is not a
/// role; it is tracked per profile in the domain.
/// </summary>
public static class AccountRoles
{
    public const string User = "User";
    public const string Administrator = "Administrator";
}
