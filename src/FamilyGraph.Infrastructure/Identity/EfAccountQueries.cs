using FamilyGraph.Application.Accounts;
using FamilyGraph.Business.People;
using FamilyGraph.Infrastructure.Persistence;
using FamilyGraph.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace FamilyGraph.Infrastructure.Identity;

internal sealed class EfAccountQueries(FamilyGraphDbContext context) : IAccountQueries
{
    public async Task<AccountList> ListAsync(int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        var administratorRoleId = IdentityConfiguration.AdministratorRole.Id;
        // One extra row tells whether the list was cut short.
        var rows = await context.Database.SqlQuery<AccountRow>($"""
            SELECT account.id AS "Id",
                account.email AS "Email",
                account.email_confirmed AS "EmailVerified",
                EXISTS (
                    SELECT 1 FROM user_account_roles AS assignment
                    WHERE assignment.user_id = account.id AND assignment.role_id = {administratorRoleId}) AS "IsAdministrator",
                (SELECT count(*)::int FROM people
                    WHERE people.maintainer_id = account.id AND people.deleted_at IS NULL) AS "MaintainedProfiles"
            FROM user_accounts AS account
            ORDER BY account.email, account.id
            LIMIT {limit + 1}
            """).ToListAsync(cancellationToken);

        var accounts = rows
            .Take(limit)
            .Select(row => new AccountSummary(
                new AccountId(row.Id), row.Email ?? "", row.EmailVerified, row.IsAdministrator, row.MaintainedProfiles))
            .ToList();
        return new AccountList(accounts, rows.Count > limit);
    }

    private sealed record AccountRow(Guid Id, string? Email, bool EmailVerified, bool IsAdministrator, int MaintainedProfiles);
}
