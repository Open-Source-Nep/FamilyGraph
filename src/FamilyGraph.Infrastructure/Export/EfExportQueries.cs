using System.Data;
using System.Runtime.CompilerServices;
using FamilyGraph.Application.Accounts;
using FamilyGraph.Application.Export;
using FamilyGraph.Business.Audit;
using FamilyGraph.Business.Handovers;
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
                SELECT account.id AS "Id", login.provider_key AS "GoogleId", account.email AS "Email",
                    account.email_confirmed AS "EmailVerified"
                FROM user_accounts AS account
                LEFT JOIN user_account_logins AS login
                    ON login.user_id = account.id AND login.login_provider = {ExternalLogin.GoogleProvider}
                ORDER BY account.id
                """).AsAsyncEnumerable();
            await foreach (var row in rows.WithCancellation(cancellationToken))
            {
                yield return new ExportedAccount(new AccountId(row.Id), row.GoogleId, row.Email ?? "", row.EmailVerified);
            }
        }

        public IAsyncEnumerable<ExportedPerson> PeopleAsync(CancellationToken cancellationToken) =>
            Stream(context.Set<Person>()
                .AsNoTracking()
                .OrderBy(person => person.Id)
                .Select(person => new ExportedPerson(
                    person.Id, person.Name, person.Visibility, person.DeletedAt, person.MaintainerId, person.Gender,
                    person.Description, person.BirthDate, person.BirthDateShown, person.ImageId)),
                cancellationToken);

        public IAsyncEnumerable<ExportedLink> LinksAsync(CancellationToken cancellationToken) =>
            Stream(context.Set<ParentChildLink>()
                .AsNoTracking()
                .OrderBy(link => link.Id)
                .Select(link => new ExportedLink(
                    link.Id, link.ParentId, link.ChildId, link.Type, link.ParentApproval, link.ChildApproval, link.BirthOrder,
                    link.DeletedAt)),
                cancellationToken);

        public IAsyncEnumerable<ExportedMarriage> MarriagesAsync(CancellationToken cancellationToken) =>
            Stream(context.Set<Marriage>()
                .AsNoTracking()
                .OrderBy(marriage => marriage.Id)
                .Select(marriage => new ExportedMarriage(
                    marriage.Id,
                    marriage.FirstSpouseId,
                    marriage.SecondSpouseId,
                    marriage.FirstApproval,
                    marriage.SecondApproval,
                    marriage.IsFormer,
                    marriage.DeletedAt)),
                cancellationToken);

        public IAsyncEnumerable<ExportedImage> ImagesAsync(CancellationToken cancellationToken) =>
            Stream(context.Set<ProfileImage>()
                .AsNoTracking()
                .OrderBy(image => image.Id)
                .Select(image => new ExportedImage(image.Id, image.PersonId, image.Format)),
                cancellationToken);

        public IAsyncEnumerable<ExportedImageContent> ImageContentsAsync(CancellationToken cancellationToken) =>
            Stream(context.Set<ProfileImage>()
                .AsNoTracking()
                .OrderBy(image => image.Id)
                .Select(image => new ExportedImageContent(image.Id, image.Format, image.Content)),
                cancellationToken);

        public IAsyncEnumerable<ExportedHandover> HandoversAsync(CancellationToken cancellationToken) =>
            Stream(context.Set<ProfileHandover>()
                .AsNoTracking()
                .OrderBy(handover => handover.Id)
                .Select(handover => new ExportedHandover(
                    handover.Id,
                    handover.PersonId,
                    handover.OfferedById,
                    handover.TokenHash,
                    handover.CreatedAt,
                    handover.ExpiresAt,
                    handover.State,
                    handover.AcceptedById,
                    handover.ClosedAt)),
                cancellationToken);

        public async IAsyncEnumerable<ExportedAuditEvent> AuditEventsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var rows = context.Set<AuditEventRecord>()
                .AsNoTracking()
                .OrderBy(record => record.OccurredAt)
                .ThenBy(record => record.Id)
                .AsAsyncEnumerable();
            await foreach (var row in rows.WithCancellation(cancellationToken))
            {
                yield return new ExportedAuditEvent(
                    row.Id, row.OccurredAt, AuditFamily.FromValue(row.Family), row.Type, new AccountId(row.ActorId), row.SubjectId, row.Data);
            }
        }

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

        private sealed record AccountRow(Guid Id, string? GoogleId, string? Email, bool EmailVerified);
    }
}
