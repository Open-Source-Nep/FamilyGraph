using System.IO.Compression;
using System.Text.Json;
using FamilyGraph.Application.Import;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Server.Accounts;
using Server.Export;

namespace Server.Import;

/// <summary>
/// An administrator's upload of an export zip into an instance without profiles (D-033, D-049). The
/// zip is copied to a temporary file, its JSON is read and checked whole, and everything is saved in
/// one transaction or not at all; images are read from the zip one at a time.
/// </summary>
[ApiController]
[Authorize]
[Route("api/import")]
public sealed class ImportController(ImportService import, ImportSettings settings) : ControllerBase
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Imports the export zip in the request body (<see cref="ExportFile"/>, format <c>familygraph-export</c>,
    /// version 2). 413 <c>import.too_large</c> beyond <c>Import:MaxFileSizeMiB</c>, 403
    /// <c>import.not_administrator</c>, 409 <c>import.instance_not_empty</c>, or 422 <c>import.invalid_file</c>
    /// with <c>problems</c> (each with <c>path</c>, <c>code</c> and <c>message</c>) and <c>moreProblems</c>.
    /// </summary>
    [HttpPost]
    [Consumes(ExportFile.ContentType)]
    [DisableRequestSizeLimit]
    public async Task<ActionResult<ImportResponse>> Import(CancellationToken cancellationToken)
    {
        if (Request.ContentLength > settings.MaxFileBytes)
        {
            return TooLarge();
        }

        if (HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
        {
            limit.MaxRequestBodySize = settings.MaxFileBytes;
        }

        await using var file = new FileStream(
            Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        try
        {
            if (!await CopyAsync(Request.Body, file, settings.MaxFileBytes, cancellationToken))
            {
                return TooLarge();
            }
        }
        catch (BadHttpRequestException e) when (e.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return TooLarge();
        }

        file.Position = 0;
        ZipArchive zip;
        try
        {
            zip = await ZipArchive.CreateAsync(file, ZipArchiveMode.Read, leaveOpen: true, entryNameEncoding: null, cancellationToken);
        }
        catch (InvalidDataException)
        {
            throw Refused("$", "import.unsupported_format", "The file is not a FamilyGraph export zip.");
        }

        await using (zip)
        {
            var entry = zip.GetEntry(ExportFile.DataEntry)
                ?? throw Refused(ExportFile.DataEntry, "import.unsupported_format", "The zip has no FamilyGraph export data.");
            ImportFileRequest? data;
            try
            {
                await using var json = await entry.OpenAsync(cancellationToken);
                data = await JsonSerializer.DeserializeAsync<ImportFileRequest>(json, Options, cancellationToken);
            }
            catch (JsonException e)
            {
                throw Refused(e.Path ?? "$", "import.invalid_json", "The file is not a FamilyGraph export: its JSON could not be read.");
            }
            catch (InvalidDataException)
            {
                throw Refused(ExportFile.DataEntry, "import.unsupported_format", "The export data in the zip is damaged.");
            }

            if (data?.Format != ExportFile.Format)
            {
                throw Refused("format", "import.unsupported_format", "The file is not a FamilyGraph export.");
            }

            if (data.Version != ExportFile.Version)
            {
                throw Refused(
                    "version",
                    "import.unsupported_version",
                    $"Only version {ExportFile.Version} export files can be imported. Export again from an updated instance.");
            }

            return ImportResponse.From(await import.ImportAsync(User.Viewer(), data.ToData(new ZipFiles(zip)), cancellationToken));
        }
    }

    private ObjectResult TooLarge() => Problem(
        statusCode: StatusCodes.Status413PayloadTooLarge,
        title: "The file is too large to import.",
        extensions: new Dictionary<string, object?> { ["code"] = "import.too_large" });

    /// <summary>Copies at most <paramref name="maxBytes"/>; false when the body is longer.</summary>
    private static async Task<bool> CopyAsync(Stream from, Stream to, long maxBytes, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await from.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                return false;
            }

            await to.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return true;
    }

    private static InvalidImportFileException Refused(string path, string code, string message) =>
        new([new ImportProblem(path, code, message)], moreProblems: false);

    /// <summary>The zip's other entries, such as images.</summary>
    private sealed class ZipFiles(ZipArchive zip) : IImportFiles
    {
        public long? LengthOf(string path) => zip.GetEntry(path)?.Length;

        public async Task<byte[]> ReadAsync(string path, CancellationToken cancellationToken)
        {
            var entry = zip.GetEntry(path) ?? throw new InvalidOperationException($"The zip has no entry {path}.");
            await using var content = await entry.OpenAsync(cancellationToken);
            using var bytes = new MemoryStream((int)Math.Min(entry.Length, int.MaxValue));

            // The declared length was checked; never read more than one byte past it.
            var buffer = new byte[81920];
            int read;
            while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
            {
                bytes.Write(buffer, 0, read);
                if (bytes.Length > entry.Length)
                {
                    break;
                }
            }

            return bytes.ToArray();
        }
    }
}
