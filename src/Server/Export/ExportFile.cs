using System.IO.Pipelines;
using System.Text.Json;
using FamilyGraph.Application.Export;

namespace Server.Export;

/// <summary>
/// Writes the export file (D-029): one JSON object with <c>format</c>, <c>version</c> and
/// <c>exportedAt</c>, then the <c>accounts</c>, <c>people</c>, <c>links</c> and <c>marriages</c> arrays, each in ID
/// order. Items are written to the response pipe as they are read and flushed every
/// <see cref="FlushThreshold"/> bytes, so the file is never held in memory. A change to the
/// layout that an import must know about raises <see cref="Version"/>.
/// </summary>
internal static class ExportFile
{
    public const string Format = "familygraph-export";

    public const int Version = 1;

    private const int FlushThreshold = 64 * 1024;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string FileName(DateTimeOffset exportedAt) => $"familygraph-export-{exportedAt:yyyy-MM-dd}.json";

    public static async Task WriteAsync(PipeWriter body, DataExport export, CancellationToken cancellationToken)
    {
        // Over a pipe the writer only fills buffers; the I/O happens in the pipe's FlushAsync.
        await using var writer = new Utf8JsonWriter(body);
        writer.WriteStartObject();
        writer.WriteString("format", Format);
        writer.WriteNumber("version", Version);
        writer.WriteString("exportedAt", export.ExportedAt);

        await WriteArrayAsync(writer, body, "accounts", export.AccountsAsync(cancellationToken), ExportedAccountItem.From, cancellationToken);
        await WriteArrayAsync(writer, body, "people", export.PeopleAsync(cancellationToken), ExportedPersonItem.From, cancellationToken);
        await WriteArrayAsync(writer, body, "links", export.LinksAsync(cancellationToken), ExportedLinkItem.From, cancellationToken);
        await WriteArrayAsync(
            writer, body, "marriages", export.MarriagesAsync(cancellationToken), ExportedMarriageItem.From, cancellationToken);

        writer.WriteEndObject();
        writer.Flush();
        await body.FlushAsync(cancellationToken);
    }

    private static async Task WriteArrayAsync<TSource, TItem>(
        Utf8JsonWriter writer,
        PipeWriter body,
        string name,
        IAsyncEnumerable<TSource> items,
        Func<TSource, TItem> toItem,
        CancellationToken cancellationToken)
    {
        writer.WriteStartArray(name);
        var flushed = writer.BytesCommitted;
        await foreach (var item in items)
        {
            JsonSerializer.Serialize(writer, toItem(item), Options);
            if (writer.BytesCommitted - flushed > FlushThreshold)
            {
                flushed = writer.BytesCommitted;
                await body.FlushAsync(cancellationToken);
            }
        }

        writer.WriteEndArray();
    }
}

/// <summary>An account by its ID, which <see cref="ExportedPersonItem.MaintainerId"/> refers to.</summary>
public sealed record ExportedAccountItem(Guid Id, string Email, bool EmailVerified)
{
    internal static ExportedAccountItem From(ExportedAccount account) =>
        new(account.Id.Value, account.Email, account.EmailVerified);
}

/// <summary>
/// A profile. <see cref="Visibility"/> is <c>Public</c> or <c>Private</c>. A deleted profile has
/// <see cref="DeletedAt"/> and no name parts. <see cref="Gender"/> is <c>Male</c>, <c>Female</c>,
/// <c>Other</c>, or null when not set (D-030).
/// </summary>
public sealed record ExportedPersonItem(
    Guid Id,
    string? FirstName,
    string? MiddleName,
    string? LastName,
    string Visibility,
    DateTimeOffset? DeletedAt,
    Guid MaintainerId,
    string? Gender)
{
    internal static ExportedPersonItem From(ExportedPerson person) => new(
        person.Id.Value,
        person.Name?.FirstName,
        person.Name?.MiddleName,
        person.Name?.LastName,
        person.Visibility.Name,
        person.DeletedAt,
        person.MaintainerId.Value,
        person.Gender?.Name);
}

/// <summary>
/// A link. <see cref="Type"/> is <c>Biological</c> or <c>Adoptive</c>; each approval is
/// <c>Pending</c>, <c>Approved</c> or <c>Disagreed</c>. <see cref="BirthOrder"/> is null while unplaced.
/// </summary>
public sealed record ExportedLinkItem(
    Guid Id,
    Guid ParentId,
    Guid ChildId,
    string Type,
    string ParentApproval,
    string ChildApproval,
    int? BirthOrder)
{
    internal static ExportedLinkItem From(ExportedLink link) => new(
        link.Id.Value,
        link.ParentId.Value,
        link.ChildId.Value,
        link.Type.Name,
        link.ParentApproval.Name,
        link.ChildApproval.Name,
        link.BirthOrder);
}

/// <summary>
/// A marriage (D-031). The spouses are in ID order; each approval is <c>Pending</c>, <c>Approved</c>
/// or <c>Disagreed</c>. <see cref="IsFormer"/> marks a marriage that ended.
/// </summary>
public sealed record ExportedMarriageItem(
    Guid Id,
    Guid FirstSpouseId,
    Guid SecondSpouseId,
    string FirstApproval,
    string SecondApproval,
    bool IsFormer)
{
    internal static ExportedMarriageItem From(ExportedMarriage marriage) => new(
        marriage.Id.Value,
        marriage.FirstSpouseId.Value,
        marriage.SecondSpouseId.Value,
        marriage.FirstApproval.Name,
        marriage.SecondApproval.Name,
        marriage.IsFormer);
}
