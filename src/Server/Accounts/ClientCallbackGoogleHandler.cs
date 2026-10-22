using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.Options;

namespace Server.Accounts;

/// <summary>
/// Google sign-in whose redirect URI is the Client's <see cref="ClientCallbackPath"/> page instead of
/// the handler's own callback path. The page passes Google's query string on to
/// <see cref="GoogleOptions.CallbackPath"/> under <c>/api</c>, so the Angular dev server proxies only
/// <c>/api</c>. Google requires the same redirect URI in the authorization request and the code
/// exchange, so both use the Client page.
/// </summary>
public sealed class ClientCallbackGoogleHandler(
    IOptionsMonitor<GoogleOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : GoogleHandler(options, logger, encoder)
{
    /// <summary>The Client route Google redirects to; it is the URI registered in the Google console.</summary>
    public const string ClientCallbackPath = "/google-callback";

    protected override string BuildChallengeUrl(AuthenticationProperties properties, string redirectUri) =>
        base.BuildChallengeUrl(properties, BuildRedirectUri(ClientCallbackPath));

    protected override Task<OAuthTokenResponse> ExchangeCodeAsync(OAuthCodeExchangeContext context) =>
        base.ExchangeCodeAsync(new OAuthCodeExchangeContext(context.Properties, context.Code, BuildRedirectUri(ClientCallbackPath)));
}
