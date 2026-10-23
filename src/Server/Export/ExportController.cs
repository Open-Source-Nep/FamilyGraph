using FamilyGraph.Application.Export;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Accounts;

namespace Server.Export;

/// <summary>
/// An administrator's download of the whole instance for a later import (D-029). Each download is
/// recorded in the audit history before the file starts.
/// </summary>
[ApiController]
[Authorize]
[Route("api/export")]
public sealed class ExportController(ExportService export) : ControllerBase
{
    /// <summary>
    /// The export file as a JSON attachment (<see cref="ExportFile"/>). A non-administrator is 403
    /// <c>export.not_administrator</c>.
    /// </summary>
    [HttpGet]
    public async Task Download(CancellationToken cancellationToken)
    {
        await using var data = await export.StartAsync(User.Viewer(), cancellationToken);
        Response.ContentType = "application/json; charset=utf-8";
        Response.Headers.ContentDisposition = $"attachment; filename=\"{ExportFile.FileName(data.ExportedAt)}\"";
        Response.Headers.CacheControl = "no-store";
        await ExportFile.WriteAsync(Response.BodyWriter, data, cancellationToken);
    }
}
