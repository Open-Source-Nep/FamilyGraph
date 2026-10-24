using FamilyGraph.Application.Import;
using FamilyGraph.Business.People;
using FamilyGraph.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FamilyGraph.Infrastructure.Import;

/// <summary>
/// The import's transaction locks the people, link and marriage tables in <c>SHARE ROW EXCLUSIVE</c>
/// mode: reads go on, but no other request can write to them, and a second import waits for the
/// first and then finds the instance no longer empty (D-033).
/// </summary>
internal sealed class EfImportStore(FamilyGraphDbContext context) : IImportStore
{
    public async Task<IImportTransaction> BeginAsync(CancellationToken cancellationToken)
    {
        var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await context.Database.ExecuteSqlRawAsync(
                "LOCK TABLE people, parent_child_links, marriages IN SHARE ROW EXCLUSIVE MODE", cancellationToken);
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

    public async Task<IReadOnlyDictionary<string, AccountId>> FindAccountsByVerifiedEmailAsync(
        IReadOnlyCollection<string> emails, CancellationToken cancellationToken)
    {
        // Identity's normalizer upper-cases the email, so the comparison ignores case.
        var normalized = emails
            .Select(email => email.Trim())
            .Where(email => email.Length > 0)
            .GroupBy(email => email.ToUpperInvariant())
            .ToDictionary(group => group.Key, group => group.ToList());
        var keys = normalized.Keys.ToList();
        var accounts = await context.Users
            .AsNoTracking()
            .Where(account => account.NormalizedEmail != null && keys.Contains(account.NormalizedEmail))
            .Select(account => new { account.Id, account.NormalizedEmail, account.EmailConfirmed })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<string, AccountId>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in accounts.GroupBy(account => account.NormalizedEmail!))
        {
            if (group.Count() == 1 && group.Single().EmailConfirmed)
            {
                foreach (var email in normalized[group.Key])
                {
                    result[email] = new AccountId(group.Single().Id);
                }
            }
        }

        return result;
    }

    private sealed class Transaction(IDbContextTransaction transaction) : IImportTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
