using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Server.Localization;

/// <summary>
/// The translations of the Client's texts, loaded before anyone signs in (D-047). Browsers
/// revalidate every time, so a deploy with new texts is picked up at the next load.
/// </summary>
[ApiController]
[Route("api/translations")]
public sealed class TranslationsController(TranslationCatalog catalog) : ControllerBase
{
    /// <summary>A translated language's messages; 404 for English, which needs none, and for any other code.</summary>
    [HttpGet("{code}")]
    public IActionResult Get(string code)
    {
        if (catalog.Find(code) is not { } translations)
        {
            return NotFound();
        }
        Response.Headers.CacheControl = "no-cache";
        return File(translations.Content, "application/json; charset=utf-8", lastModified: null, new EntityTagHeaderValue(translations.ETag));
    }
}
