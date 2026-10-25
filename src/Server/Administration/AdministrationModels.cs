using System.ComponentModel.DataAnnotations;
using FamilyGraph.Application.Accounts;

namespace Server.Administration;

/// <summary>The account that becomes the maintainer (D-035).</summary>
public sealed class TransferRequest
{
    [Required]
    public Guid? ToAccountId { get; init; }
}

/// <summary>An account on the Users page; there is nothing else stored about it (D-018).</summary>
public sealed record AccountSummaryResponse(Guid Id, string Email, bool EmailVerified, bool Administrator, int MaintainedProfiles);

/// <summary><see cref="IsTruncated"/> says that the list stopped at its limit (D-010).</summary>
public sealed record AccountListResponse(IReadOnlyList<AccountSummaryResponse> Accounts, bool IsTruncated)
{
    internal static AccountListResponse From(AccountList list) => new(
        [.. list.Accounts.Select(account => new AccountSummaryResponse(
            account.Id.Value, account.Email, account.EmailVerified, account.IsAdministrator, account.MaintainedProfiles))],
        list.IsTruncated);
}

/// <summary>How many profiles moved to the other account.</summary>
public sealed record TransferredProfilesResponse(int Transferred);
