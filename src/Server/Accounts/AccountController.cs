using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FamilyGraph.Application.Accounts;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Server.Accounts;

/// <summary>
/// Sign-in with Google, sign-out and the signed-in user's account (#11, D-008). The first sign-in
/// registers the account; it never creates a person profile (PRD 01).
/// </summary>
[ApiController]
[Route("api/account")]
/// <remarks>
/// <see cref="AccountService"/> is resolved only once an action needs it, so a signed-out
/// <c>GET /api/account</c> and sign-in without Google configured still answer on an instance that
/// has no database either.
/// </remarks>
public sealed class AccountController(IAntiforgery antiforgery) : ControllerBase
{
    /// <summary>The cookie Angular's HttpClient reads and echoes in the <c>X-XSRF-TOKEN</c> header (D-011).</summary>
    internal const string XsrfCookie = "XSRF-TOKEN";

    /// <summary>The login provider name stored with each account's Google user ID.</summary>
    internal const string GoogleProvider = "Google";

    /// <summary>
    /// The signed-in user's account, or 401. Both answers set the antiforgery cookie, so a signed-out
    /// Client can send the sign-in request (D-011).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<AccountResponse>> Get(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            IssueAntiforgeryToken();
            return Unauthorized();
        }

        var account = await Accounts.GetAsync(User.AccountId(), cancellationToken);
        if (account is null)
        {
            // The account behind the session no longer exists.
            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
            IssueAntiforgeryToken();
            return Unauthorized();
        }

        IssueAntiforgeryToken();
        return AccountResponse.From(account);
    }

    /// <summary>
    /// Signs in with the ID token the Client got from Google: checks it, creates or updates the
    /// account and starts the session.
    /// </summary>
    [HttpPost("google-sign-in")]
    public async Task<IActionResult> SignInWithGoogle(
        GoogleSignInRequest request,
        [FromServices] GoogleSignInSettings google,
        [FromServices] IGoogleIdTokenValidator idTokens,
        CancellationToken cancellationToken)
    {
        if (!google.Enabled)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Google sign-in is not configured on this instance.",
                extensions: new Dictionary<string, object?> { ["code"] = "account.sign_in_unavailable" });
        }

        var token = await idTokens.ValidateAsync(request.IdToken);
        if (token is null || token.Nonce != NonceFor(request.NonceSecret))
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Sign-in did not complete.",
                extensions: new Dictionary<string, object?> { ["code"] = "account.sign_in_failed" });
        }

        var account = await Accounts.SignInAsync(
            new ExternalLogin(GoogleProvider, token.Subject, token.Email, token.EmailVerified), cancellationToken);

        var identity = new ClaimsIdentity(IdentityConstants.ApplicationScheme, ClaimTypes.NameIdentifier, ClaimTypes.Role);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, account.Id.Value.ToString()));
        identity.AddClaims(account.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, new ClaimsPrincipal(identity));

        return NoContent();
    }

    /// <summary>Ends the session. Protected against CSRF like every state-changing request (D-011).</summary>
    [HttpPost("sign-out")]
    public async Task<IActionResult> SignOutOfSession()
    {
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        Response.Cookies.Delete(XsrfCookie);
        return NoContent();
    }

    private AccountService Accounts => HttpContext.RequestServices.GetRequiredService<AccountService>();

    /// <summary>The nonce the Client sent to Google: base64url of the SHA-256 of its secret.</summary>
    internal static string NonceFor(string secret) =>
        Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    private void IssueAntiforgeryToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        Response.Cookies.Append(XsrfCookie, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Strict,
        });
    }
}
