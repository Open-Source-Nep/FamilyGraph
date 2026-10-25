using System.Data;
using System.Runtime.CompilerServices;
using FamilyGraph.Application.Export;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;
using FamilyGraph.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FamilyGraph.Infrastructure.Export;

/// <summary>
/// The snapshot is a read-only repeatable-read transaction, which PostgreSQL serves from one
/// snapshot for all its statements, so the sections agree with each other (D-029).
/// </summary>
internal sealed class EfExportQueries(FamilyGraphDbContext context) : IExportQueries
{
    public async Task<IExportSnapshot> OpenSnapshotAsync(CancellationToken cancellationToken)
    {
        var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        try
        {
            await context.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", cancellationToken);
            return new Snapshot(context, transaction);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private sealed class Snapshot(FamilyGraphDbContext context, IDbContextTransaction transaction) : IExportSnapshot
    {
        public async IAsyncEnumerable<ExportedAccount> AccountsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var rows = context.Database.SqlQuery<AccountRow>($"""
                SELECT account.id AS "Id", account.email AS "Email", account.email_confirmed AS "EmailVerified"
                FROM user_accounts AS account
                WHERE EXISTS (SELECT 1 FROM people WHERE people.maintainer_id = account.id)
                ORDER BY account.id
                """).AsAsyncEnumerable();
            await foreach (var row in rows.WithCancellation(cancellationToken))
            {
                yield return new ExportedAccount(new AccountId(row.Id), row.Email ?? "", row.EmailVerified);
            }
        }

        public IAsyncEnumerable<ExportedPerson> PeopleAsync(CancellationToken cancellationToken) =>
            Stream(context.Set<Person>()
                .AsNoTracking()
                .OrderBy(person => person.Id)
                .Select(person => new ExportedPerson(
                    person.Id, person.Name, person.Visibility, person.DeletedAt, person.MaintainerId, person.Gender,
                    person.Description, person.BirthDate, person.BirthDateShown)),
                cancellationToken);

        public IAsyncEnumerable<ExportedLink> LinksAsync(CancellationToken cancellationToken) =>
            Stream(context.Set<ParentChildLink>()
                .AsNoTracking()
                .Where(link => link.DeletedAt == null)
                .OrderBy(link => link.Id)
                .Select(link => new ExportedLink(
                    link.Id, link.ParentId, link.ChildId, link.Type, link.ParentApproval, link.ChildApproval, link.BirthOrder)),
                cancellationToken);

        public IAsyncEnumerable<ExportedMarriage> MarriagesAsync(CancellationToken cancellationToken) =>
            Stream(context.Set<Marriage>()
                .AsNoTracking()
                .Where(marriage => marriage.DeletedAt == null)
                .OrderBy(marriage => marriage.Id)
                .Select(marriage => new ExportedMarriage(
                    marriage.Id,
                    marriage.FirstSpouseId,
                    marriage.SecondSpouseId,
                    marriage.FirstApproval,
                    marriage.SecondApproval,
                    marriage.IsFormer)),
                cancellationToken);

        private static async IAsyncEnumerable<T> Stream<T>(
            IQueryable<T> query, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var item in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
            {
                yield return item;
            }
        }

        /// <summary>Ends the read-only transaction; there is nothing to commit.</summary>
        public ValueTask DisposeAsync() => transaction.DisposeAsync();

        private sealed record AccountRow(Guid Id, string? Email, bool EmailVerified);
    }
}
