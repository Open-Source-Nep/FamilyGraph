using System.Text.RegularExpressions;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Net.Http.Headers;

namespace Server.Client;

/// <summary>
/// Caching for the built Client in wwwroot. The Angular build puts a content hash in every script
/// and stylesheet name, so those never change and can be cached for good. Everything else, above
/// all index.html, is revalidated on every load: a cached index.html would keep naming the previous
/// deploy's chunks, which no longer exist.
/// </summary>
public static partial class ClientFiles
{
    private const string Immutable = "public, max-age=31536000, immutable";
    private const string Revalidate = "no-cache";

    public static StaticFileOptions StaticFileOptions() => new()
    {
        OnPrepareResponse = context =>
            context.Context.Response.Headers[HeaderNames.CacheControl] =
                IsHashed(context.File.Name) ? Immutable : Revalidate,
    };

    /// <summary>Angular's hashed output, such as <c>main-DRHZNV2K.js</c> or <c>chunk-D_uRply_.js</c>.</summary>
    public static bool IsHashed(string fileName) => HashedName().IsMatch(fileName);

    [GeneratedRegex(@"-[A-Za-z0-9_-]{8}\.(js|css)$")]
    private static partial Regex HashedName();
}
