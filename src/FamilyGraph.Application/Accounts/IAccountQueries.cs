using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Accounts;

/// <summary>Read models of user accounts for administrators (D-035). Every list takes a limit (D-010).</summary>
public interface IAccountQueries
{
    /// <summary>
    /// Every account with how many non-deleted profiles it maintains, sorted by email and then ID, at
    /// most <paramref name="limit"/> of them.
    /// </summary>
    Task<AccountList> ListAsync(int limit, CancellationToken cancellationToken);
}

/// <summary>An account on the administrator's Users page (D-035). There is nothing else to show (D-018).</summary>
public sealed record AccountSummary(AccountId Id, string Email, bool EmailVerified, bool IsAdministrator, int MaintainedProfiles);

/// <summary><see cref="IsTruncated"/> says that the list stopped at its limit (D-010).</summary>
public sealed record AccountList(IReadOnlyList<AccountSummary> Accounts, bool IsTruncated);
