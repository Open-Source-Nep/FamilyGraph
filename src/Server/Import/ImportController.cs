using System.Text.Json;
using FamilyGraph.Application.Import;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Accounts;
using Server.Export;

namespace Server.Import;

/// <summary>
/// An administrator's upload of an export file into an instance without profiles (D-033). The
/// file is read whole, checked whole, and saved in one transaction or not at all.
/// </summary>
[ApiController]
[Authorize]
[Route("api/import")]
public sealed class ImportController(ImportService import) : ControllerBase
{
    /// <summary>The largest file read; a larger body is refused (413) before it is read. About 200,000 profiles fit.</summary>
    public const long MaxFileBytes = 64 * 1024 * 1024;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Imports the export file in the request body (<see cref="ExportFile"/>, format
    /// <c>familygraph-export</c>, version 1). 403 <c>import.not_administrator</c>, 409
    /// <c>import.instance_not_empty</c>, or 422 <c>import.invalid_file</c> with <c>problems</c> (each
    /// with <c>path</c>, <c>code</c> and <c>message</c>) and <c>moreProblems</c>.
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [RequestSizeLimit(MaxFileBytes)]
    public async Task<ImportResponse> Import(CancellationToken cancellationToken)
    {
        ImportFileRequest? file;
        try
        {
            file = await JsonSerializer.DeserializeAsync<ImportFileRequest>(Request.Body, Options, cancellationToken);
        }
        catch (JsonException e)
        {
            throw Refused(e.Path ?? "$", "import.invalid_json", "The file is not a FamilyGraph export: its JSON could not be read.");
        }

        if (file?.Format != ExportFile.Format)
        {
            throw Refused("format", "import.unsupported_format", "The file is not a FamilyGraph export.");
        }

        if (file.Version != ExportFile.Version)
        {
            throw Refused("version", "import.unsupported_version", $"Only version {ExportFile.Version} export files can be imported.");
        }

        return ImportResponse.From(await import.ImportAsync(User.Viewer(), file.ToData(), cancellationToken));
    }

    private static InvalidImportFileException Refused(string path, string code, string message) =>
        new([new ImportProblem(path, code, message)], moreProblems: false);
}
