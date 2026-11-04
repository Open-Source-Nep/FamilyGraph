using System.IO.Compression;
using System.Text.Json;
using FamilyGraph.Application.Export;
using FamilyGraph.Business.People;

namespace Server.Export;

/// <summary>
/// Writes the export file (D-029, D-049): a zip with <see cref="DataEntry"/>, one JSON object with
/// <c>format</c>, <c>version</c> and <c>exportedAt</c>, then the <c>accounts</c>, <c>people</c>, <c>links</c>,
/// <c>marriages</c>, <c>images</c>, <c>handovers</c>, <c>claims</c> and <c>auditEvents</c> arrays, each in ID order (the
/// audit history oldest first); then each image's bytes as <c>images/&lt;id&gt;.&lt;format&gt;</c>. Rows are
/// written as they are read and flushed every <see cref="FlushThreshold"/> bytes, so neither the data nor the
/// zip is ever held in memory. A change to the layout that an import must know about raises <see cref="Version"/>.
/// </summary>
internal static class ExportFile
{
    public const string Format = "familygraph-export";

    /// <summary>2 since D-049: a zip with every account, images, deleted rows, handovers and audit history.</summary>
    public const int Version = 2;

    /// <summary>The JSON entry in the zip.</summary>
    public const string DataEntry = "familygraph.json";

    public const string ContentType = "application/zip";

    private const int FlushThreshold = 64 * 1024;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string FileName(DateTimeOffset exportedAt) => $"familygraph-export-{exportedAt:yyyy-MM-dd}.zip";

    /// <summary>Where an image's bytes are in the zip.</summary>
    public static string ImagePath(Guid id, ImageFormat format) => $"images/{id}.{format.Name.ToLowerInvariant()}";

    public static async Task WriteAsync(Stream body, DataExport export, CancellationToken cancellationToken)
    {
        await using var zip = await ZipArchive.CreateAsync(body, ZipArchiveMode.Create, leaveOpen: true, entryNameEncoding: null, cancellationToken);

        var entry = zip.CreateEntry(DataEntry, CompressionLevel.Fastest);
        entry.LastWriteTime = export.ExportedAt;
        await using (var data = await entry.OpenAsync(cancellationToken))
        {
            await WriteDataAsync(data, export, cancellationToken);
        }

        await foreach (var image in export.ImageContentsAsync(cancellationToken))
        {
            // Images are already compressed.
            var imageEntry = zip.CreateEntry(ImagePath(image.Id.Value, image.Format), CompressionLevel.NoCompression);
            imageEntry.LastWriteTime = export.ExportedAt;
            await using var content = await imageEntry.OpenAsync(cancellationToken);
            await content.WriteAsync(image.Content, cancellationToken);
        }
    }

    private static async Task WriteDataAsync(Stream data, DataExport export, CancellationToken cancellationToken)
    {
        await using var writer = new Utf8JsonWriter(data);
        writer.WriteStartObject();
        writer.WriteString("format", Format);
        writer.WriteNumber("version", Version);
        writer.WriteString("exportedAt", export.ExportedAt);

        await WriteArrayAsync(writer, "accounts", export.AccountsAsync(cancellationToken), ExportedAccountItem.From, cancellationToken);
        await WriteArrayAsync(writer, "people", export.PeopleAsync(cancellationToken), ExportedPersonItem.From, cancellationToken);
        await WriteArrayAsync(writer, "links", export.LinksAsync(cancellationToken), ExportedLinkItem.From, cancellationToken);
        await WriteArrayAsync(writer, "marriages", export.MarriagesAsync(cancellationToken), ExportedMarriageItem.From, cancellationToken);
        await WriteArrayAsync(writer, "images", export.ImagesAsync(cancellationToken), ExportedImageItem.From, cancellationToken);
        await WriteArrayAsync(writer, "handovers", export.HandoversAsync(cancellationToken), ExportedHandoverItem.From, cancellationToken);
        await WriteArrayAsync(writer, "claims", export.ClaimsAsync(cancellationToken), ExportedClaimItem.From, cancellationToken);
        await WriteArrayAsync(
            writer, "auditEvents", export.AuditEventsAsync(cancellationToken), ExportedAuditEventItem.From, cancellationToken);

        writer.WriteEndObject();
        await writer.FlushAsync(cancellationToken);
    }

    private static async Task WriteArrayAsync<TSource, TItem>(
        Utf8JsonWriter writer,
        string name,
        IAsyncEnumerable<TSource> items,
        Func<TSource, TItem> toItem,
        CancellationToken cancellationToken)
    {
        writer.WriteStartArray(name);
        await foreach (var item in items)
        {
            JsonSerializer.Serialize(writer, toItem(item), Options);
            if (writer.BytesPending > FlushThreshold)
            {
                await writer.FlushAsync(cancellationToken);
            }
        }

        writer.WriteEndArray();
    }
}

/// <summary>
/// An account by its ID, which every other account reference in the file uses (D-049). <see cref="GoogleId"/>
/// is the Google login's subject, so the import can recreate an account its owner signs in to again.
/// </summary>
public sealed record ExportedAccountItem(Guid Id, string? GoogleId, string Email, bool EmailVerified)
{
    internal static ExportedAccountItem From(ExportedAccount account) =>
        new(account.Id.Value, account.GoogleId, account.Email, account.EmailVerified);
}

/// <summary>
/// A profile. <see cref="Visibility"/> is <c>Public</c> or <c>Private</c>. A deleted profile has
/// <see cref="DeletedAt"/> and no name parts. <see cref="Gender"/> is <c>Male</c>, <c>Female</c>,
/// <c>Other</c>, or null when not set (D-030). <see cref="Description"/> and <see cref="BirthDate"/> are null
/// when not set; <see cref="BirthDateShown"/> is the maintainer's switch (D-037). <see cref="ImageId"/> names the
/// profile's image in <c>images</c> (D-049). <see cref="ClaimedById"/> is the account of the person the
/// profile describes, once claimed (D-051).
/// </summary>
public sealed record ExportedPersonItem(
    Guid Id,
    string? FirstName,
    string? MiddleName,
    string? LastName,
    string Visibility,
    DateTimeOffset? DeletedAt,
    Guid MaintainerId,
    string? Gender,
    string? Description,
    ExportedBirthDateItem? BirthDate,
    bool BirthDateShown,
    Guid? ImageId,
    Guid? ClaimedById)
{
    internal static ExportedPersonItem From(ExportedPerson person) => new(
        person.Id.Value,
        person.Name?.FirstName,
        person.Name?.MiddleName,
        person.Name?.LastName,
        person.Visibility.Name,
        person.DeletedAt,
        person.MaintainerId.Value,
        person.Gender?.Name,
        person.Description,
        person.BirthDate is { } date ? new ExportedBirthDateItem(date.Calendar.Name, date.Year, date.Month, date.Day) : null,
        person.BirthDateShown,
        person.ImageId?.Value,
        person.ClaimedById?.Value);
}

/// <summary>A birth date as entered: <c>Bs</c> or <c>Ad</c>, a year, and the month and day if known (D-037).</summary>
public sealed record ExportedBirthDateItem(string Calendar, int Year, int? Month, int? Day);

/// <summary>
/// A link. <see cref="Type"/> is <c>Biological</c> or <c>Adoptive</c>; each approval is
/// <c>Pending</c>, <c>Approved</c> or <c>Disagreed</c>. <see cref="BirthOrder"/> is null while unplaced;
/// <see cref="DeletedAt"/> is set on a deleted link (D-049).
/// </summary>
public sealed record ExportedLinkItem(
    Guid Id,
    Guid ParentId,
    Guid ChildId,
    string Type,
    string ParentApproval,
    string ChildApproval,
    int? BirthOrder,
    DateTimeOffset? DeletedAt)
{
    internal static ExportedLinkItem From(ExportedLink link) => new(
        link.Id.Value,
        link.ParentId.Value,
        link.ChildId.Value,
        link.Type.Name,
        link.ParentApproval.Name,
        link.ChildApproval.Name,
        link.BirthOrder,
        link.DeletedAt);
}

/// <summary>
/// A marriage (D-031). The spouses are in ID order; each approval is <c>Pending</c>, <c>Approved</c>
/// or <c>Disagreed</c>. <see cref="IsFormer"/> marks a marriage that ended; <see cref="DeletedAt"/> a deleted one (D-049).
/// </summary>
public sealed record ExportedMarriageItem(
    Guid Id,
    Guid FirstSpouseId,
    Guid SecondSpouseId,
    string FirstApproval,
    string SecondApproval,
    bool IsFormer,
    DateTimeOffset? DeletedAt)
{
    internal static ExportedMarriageItem From(ExportedMarriage marriage) => new(
        marriage.Id.Value,
        marriage.FirstSpouseId.Value,
        marriage.SecondSpouseId.Value,
        marriage.FirstApproval.Name,
        marriage.SecondApproval.Name,
        marriage.IsFormer,
        marriage.DeletedAt);
}

/// <summary>A profile image (D-028): <see cref="Format"/> is <c>Jpeg</c>, <c>Png</c> or <c>WebP</c>, and <see cref="File"/> its path in the zip.</summary>
public sealed record ExportedImageItem(Guid Id, Guid PersonId, string Format, string File)
{
    internal static ExportedImageItem From(ExportedImage image) =>
        new(image.Id.Value, image.PersonId.Value, image.Format.Name, ExportFile.ImagePath(image.Id.Value, image.Format));
}

/// <summary>
/// A handover link (D-027): only the SHA-256 hash of its secret. <see cref="State"/> is <c>Open</c>,
/// <c>Accepted</c> or <c>Cancelled</c>.
/// </summary>
public sealed record ExportedHandoverItem(
    Guid Id,
    Guid PersonId,
    Guid OfferedById,
    string TokenHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    string State,
    Guid? AcceptedById,
    DateTimeOffset? ClosedAt)
{
    internal static ExportedHandoverItem From(ExportedHandover handover) => new(
        handover.Id.Value,
        handover.PersonId.Value,
        handover.OfferedById.Value,
        handover.TokenHash,
        handover.CreatedAt,
        handover.ExpiresAt,
        handover.State.Name,
        handover.AcceptedById?.Value,
        handover.ClosedAt);
}

/// <summary>
/// An identity claim with its evidence note (D-051). <see cref="State"/> is <c>Open</c>, <c>Approved</c>,
/// <c>Rejected</c>, <c>Withdrawn</c> or <c>Cancelled</c>.
/// </summary>
public sealed record ExportedClaimItem(
    Guid Id,
    Guid PersonId,
    Guid ClaimantId,
    string? Evidence,
    DateTimeOffset SubmittedAt,
    string State,
    Guid? ClosedById,
    DateTimeOffset? ClosedAt)
{
    internal static ExportedClaimItem From(ExportedClaim claim) => new(
        claim.Id.Value,
        claim.PersonId.Value,
        claim.ClaimantId.Value,
        claim.Evidence,
        claim.SubmittedAt,
        claim.State.Name,
        claim.ClosedById?.Value,
        claim.ClosedAt);
}

/// <summary>
/// An audit history row as stored (D-015): <see cref="Family"/> by name, <see cref="Type"/> the event's name,
/// and <see cref="Data"/> its other fields, with IDs only and value sets as numbers.
/// </summary>
public sealed record ExportedAuditEventItem(
    Guid Id,
    DateTimeOffset OccurredAt,
    string Family,
    string Type,
    Guid ActorId,
    Guid SubjectId,
    JsonElement Data)
{
    internal static ExportedAuditEventItem From(ExportedAuditEvent auditEvent)
    {
        using var data = JsonDocument.Parse(auditEvent.Data);
        return new(
            auditEvent.Id,
            auditEvent.OccurredAt,
            auditEvent.Family.Name,
            auditEvent.Type,
            auditEvent.ActorId.Value,
            auditEvent.SubjectId,
            data.RootElement.Clone());
    }
}
