namespace FamilyGraph.Application.Export;

/// <summary>Reads the whole instance for an export (D-029). The only read that is not bounded by a limit.</summary>
public interface IExportQueries
{
    /// <summary>
    /// Opens a consistent, read-only view of the data: every section reads the same moment, so
    /// each link's people and each person's maintainer are in the export.
    /// </summary>
    Task<IExportSnapshot> OpenSnapshotAsync(CancellationToken cancellationToken);
}

/// <summary>
/// One moment's data, streamed in ID order so an instance of any size never sits in memory. Read
/// the sections one after another, not at the same time.
/// </summary>
public interface IExportSnapshot : IAsyncDisposable
{
    /// <summary>The accounts that maintain at least one profile, deleted or not.</summary>
    IAsyncEnumerable<ExportedAccount> AccountsAsync(CancellationToken cancellationToken);

    /// <summary>Every profile, with its stored name even when deleted.</summary>
    IAsyncEnumerable<ExportedPerson> PeopleAsync(CancellationToken cancellationToken);

    /// <summary>Every non-deleted link.</summary>
    IAsyncEnumerable<ExportedLink> LinksAsync(CancellationToken cancellationToken);

    /// <summary>Every non-deleted marriage (D-031).</summary>
    IAsyncEnumerable<ExportedMarriage> MarriagesAsync(CancellationToken cancellationToken);
}
