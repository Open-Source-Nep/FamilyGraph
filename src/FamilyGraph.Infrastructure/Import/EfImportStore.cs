using FamilyGraph.Application.Accounts;
using FamilyGraph.Application.Import;
using FamilyGraph.Business.People;
using FamilyGraph.Infrastructure.Identity;
using FamilyGraph.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FamilyGraph.Infrastructure.Import;

/// <summary>
/// The import's transaction locks the people, link, marriage, image and handover tables in
/// <c>SHARE ROW EXCLUSIVE</c> mode: reads go on, but no other request can write to them, and a second
/// import waits for the first and then finds the instance no longer empty (D-033). Accounts, images and
/// audit rows are saved as they are added and then let go, so the change tracker never holds them all (D-049).
/// </summary>
internal sealed class EfImportStore(FamilyGraphDbContext context, UserManager<UserAccount> users) : IImportStore
{
    public async Task<IImportTransaction> BeginAsync(CancellationToken cancellationToken)
    {
        var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await context.Database.ExecuteSqlRawAsync(
                "LOCK TABLE people, parent_child_links, marriages, profile_images, profile_handovers IN SHARE ROW EXCLUSIVE MODE",
                cancellationToken);
            return new Transaction(transaction);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    public Task<bool> HasPeopleAsync(CancellationToken cancellationToken) =>
        context.Set<Person>().AnyAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<string, AccountId>> FindAccountsByGoogleIdAsync(
        IReadOnlyCollection<string> googleIds, CancellationToken cancellationToken)
    {
        var keys = googleIds.Distinct().ToList();
        var logins = await context.Set<IdentityUserLogin<Guid>>()
            .AsNoTracking()
            .Where(login => login.LoginProvider == ExternalLogin.GoogleProvider && keys.Contains(login.ProviderKey))
            .Select(login => new { login.ProviderKey, login.UserId })
            .ToListAsync(cancellationToken);
        return logins.ToDictionary(login => login.ProviderKey, login => new AccountId(login.UserId), StringComparer.Ordinal);
    }

    public async Task<IReadOnlySet<AccountId>> FindExistingAccountsAsync(
        IReadOnlyCollection<AccountId> ids, CancellationToken cancellationToken)
    {
        var keys = ids.Select(id => id.Value).ToList();
        var existing = await context.Users
            .AsNoTracking()
            .Where(account => keys.Contains(account.Id))
            .Select(account => account.Id)
            .ToListAsync(cancellationToken);
        return existing.Select(id => new AccountId(id)).ToHashSet();
    }

    public async Task CreateAccountAsync(ImportedAccount account, CancellationToken cancellationToken)
    {
        // As at a first sign-in (IdentityAccountStore); the administrator role follows the configuration at the next one (D-008).
        var user = new UserAccount
        {
            Id = account.Id.Value,
            UserName = account.Id.Value.ToString("N"),
            Email = account.Email,
            EmailConfirmed = account.EmailVerified,
        };
        Check(await users.CreateAsync(user));
        if (account.GoogleId is { } googleId)
        {
            Check(await users.AddLoginAsync(
                user, new UserLoginInfo(ExternalLogin.GoogleProvider, googleId, ExternalLogin.GoogleProvider)));
        }

        Check(await users.AddToRoleAsync(user, AccountRoles.User));
        context.Entry(user).State = EntityState.Detached;
    }

    public async Task AddImageAsync(ProfileImage image, CancellationToken cancellationToken)
    {
        context.Add(image);
        await context.SaveChangesAsync(cancellationToken);
        context.Entry(image).State = EntityState.Detached;
    }

    public async Task AddAuditEventsAsync(IReadOnlyList<ImportedAuditEvent> auditEvents, CancellationToken cancellationToken)
    {
        var records = auditEvents
            .Select(row => AuditEventRecord.Restore(
                row.Id, row.OccurredAt, row.Family.Value, row.Type, row.ActorId.Value, row.SubjectId, row.Data))
            .ToList();
        context.AddRange(records);
        await context.SaveChangesAsync(cancellationToken);
        foreach (var record in records)
        {
            context.Entry(record).State = EntityState.Detached;
        }
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Account import failed: " + string.Join("; ", result.Errors.Select(error => error.Code)));
        }
    }

    private sealed class Transaction(IDbContextTransaction transaction) : IImportTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
