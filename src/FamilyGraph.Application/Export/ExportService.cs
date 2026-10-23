using System.Runtime.CompilerServices;
using FamilyGraph.Application.Persistence;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.Export;

namespace FamilyGraph.Application.Export;

/// <summary>
/// An administrator's export of the whole instance for a later import (D-029): every profile,
/// private ones with their names, every non-deleted link with both endpoints' consent, and the
/// emails of the accounts that maintain them. No images, audit history or deleted links.
/// </summary>
public sealed class ExportService(IExportQueries queries, IAuditLog audit, IUnitOfWork unitOfWork, TimeProvider time)
{
    /// <summary>
    /// Records the export in the audit history, then opens the data to stream. A non-administrator
    /// is 403 <c>export.not_administrator</c> and nothing is recorded.
    /// </summary>
    public async Task<DataExport> StartAsync(Viewer viewer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);

        if (!viewer.IsAdministrator)
        {
            throw new NotAdministratorException();
        }

        var now = time.GetUtcNow();
        audit.Add([new DataExported(viewer.AccountId)], now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new DataExport(now, await queries.OpenSnapshotAsync(cancellationToken));
    }
}

/// <summary>An export in progress. Read the sections in order, then dispose it.</summary>
public sealed class DataExport(DateTimeOffset exportedAt, IExportSnapshot snapshot) : IAsyncDisposable
{
    public DateTimeOffset ExportedAt { get; } = exportedAt;

    public IAsyncEnumerable<ExportedAccount> AccountsAsync(CancellationToken cancellationToken) =>
        snapshot.AccountsAsync(cancellationToken);

    /// <summary>Every profile; a deleted one has no name, so the erased marker never leaves the server.</summary>
    public async IAsyncEnumerable<ExportedPerson> PeopleAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var person in snapshot.PeopleAsync(cancellationToken))
        {
            yield return person.DeletedAt is null ? person : person with { Name = null };
        }
    }

    public IAsyncEnumerable<ExportedLink> LinksAsync(CancellationToken cancellationToken) =>
        snapshot.LinksAsync(cancellationToken);

    public ValueTask DisposeAsync() => snapshot.DisposeAsync();
}
