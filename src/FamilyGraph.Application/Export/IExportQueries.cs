namespace FamilyGraph.Application.Export;

/// <summary>Reads the whole instance for an export (D-029). The only read that is not bounded by a limit.</summary>
public interface IExportQueries
{
    /// <summary>
    /// Opens a consistent, read-only view of the data: every section reads the same moment, so
    /// each row's people and accounts are in the export.
    /// </summary>
    Task<IExportSnapshot> OpenSnapshotAsync(CancellationToken cancellationToken);
}

/// <summary>
/// One moment's data, streamed in ID order so an instance of any size never sits in memory. Read
/// the sections one after another, not at the same time.
/// </summary>
public interface IExportSnapshot : IAsyncDisposable
{
    /// <summary>Every account (D-049).</summary>
    IAsyncEnumerable<ExportedAccount> AccountsAsync(CancellationToken cancellationToken);

    /// <summary>Every profile, with its stored name even when deleted.</summary>
    IAsyncEnumerable<ExportedPerson> PeopleAsync(CancellationToken cancellationToken);

    /// <summary>Every link, deleted ones too (D-049).</summary>
    IAsyncEnumerable<ExportedLink> LinksAsync(CancellationToken cancellationToken);

    /// <summary>Every marriage, deleted ones too (D-031, D-049).</summary>
    IAsyncEnumerable<ExportedMarriage> MarriagesAsync(CancellationToken cancellationToken);

    /// <summary>Every profile image, without its content.</summary>
    IAsyncEnumerable<ExportedImage> ImagesAsync(CancellationToken cancellationToken);

    /// <summary>Every profile image's content, one row at a time.</summary>
    IAsyncEnumerable<ExportedImageContent> ImageContentsAsync(CancellationToken cancellationToken);

    /// <summary>Every handover link.</summary>
    IAsyncEnumerable<ExportedHandover> HandoversAsync(CancellationToken cancellationToken);

    /// <summary>Every identity claim (D-051).</summary>
    IAsyncEnumerable<ExportedClaim> ClaimsAsync(CancellationToken cancellationToken);

    /// <summary>The whole audit history, oldest first.</summary>
    IAsyncEnumerable<ExportedAuditEvent> AuditEventsAsync(CancellationToken cancellationToken);
}
