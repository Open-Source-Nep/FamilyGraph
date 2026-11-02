using System.Text.Json;
using FamilyGraph.Application.Import;

namespace Server.Import;

/// <summary>
/// The export file's <c>familygraph.json</c> as read back (D-029, D-049). Missing sections are empty;
/// unknown fields are ignored. Values are checked by <see cref="ImportService"/>, so each problem is
/// reported with its place.
/// </summary>
public sealed record ImportFileRequest(
    string? Format,
    int? Version,
    IReadOnlyList<ImportAccount>? Accounts,
    IReadOnlyList<ImportPerson>? People,
    IReadOnlyList<ImportLink>? Links,
    IReadOnlyList<ImportMarriage>? Marriages,
    IReadOnlyList<ImportImage>? Images,
    IReadOnlyList<ImportHandover>? Handovers,
    IReadOnlyList<ImportAuditEventRequest>? AuditEvents)
{
    internal ImportData ToData(IImportFiles files) => new(
        Accounts ?? [],
        People ?? [],
        Links ?? [],
        Marriages ?? [],
        Images ?? [],
        Handovers ?? [],
        (AuditEvents ?? []).Select(row => row.ToRow()).ToList(),
        files);
}

/// <summary>An audit row with its <see cref="Data"/> as written, which must be a JSON object.</summary>
public sealed record ImportAuditEventRequest(
    Guid Id,
    DateTimeOffset OccurredAt,
    string? Family,
    string? Type,
    Guid ActorId,
    Guid SubjectId,
    JsonElement Data)
{
    internal ImportAuditEvent ToRow() => new(
        Id, OccurredAt, Family, Type, ActorId, SubjectId, Data.ValueKind == JsonValueKind.Object ? Data.GetRawText() : null);
}

/// <summary>What was imported, and how many file accounts were created or matched an account here by Google ID.</summary>
public sealed record ImportResponse(
    int People,
    int Links,
    int Marriages,
    int Images,
    int Handovers,
    int AuditEvents,
    int CreatedAccounts,
    int ReusedAccounts)
{
    internal static ImportResponse From(ImportResult result) => new(
        result.People,
        result.Links,
        result.Marriages,
        result.Images,
        result.Handovers,
        result.AuditEvents,
        result.CreatedAccounts,
        result.ReusedAccounts);
}

/// <summary>The <c>Import</c> settings.</summary>
/// <param name="MaxFileBytes">The largest export zip read (<c>Import:MaxFileSizeMiB</c>, default 1024).</param>
public sealed record ImportSettings(long MaxFileBytes)
{
    public const int DefaultMaxFileSizeMiB = 1024;
}
