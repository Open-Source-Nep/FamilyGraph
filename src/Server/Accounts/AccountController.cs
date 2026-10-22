using System.Security.Claims;
using FamilyGraph.Application.Accounts;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
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
/// <see cref="AccountService"/> is injected per action, so starting sign-in still answers 503 when
/// Google is not configured on an instance that has no database either.
/// </remarks>
public sealed class AccountController(IAntiforgery antiforgery) : ControllerBase
{
    /// <summary>The cookie Angular's HttpClient reads and echoes in the <c>X-XSRF-TOKEN</c> header (D-011).</summary>
    internal const string XsrfCookie = "XSRF-TOKEN";

    internal const string SignInFailedTitle = "Sign-in did not complete.";
    internal const string SignInFailedCode = "account.sign_in_failed";

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<AccountResponse>> Get([FromServices] AccountService accounts, CancellationToken cancellationToken)
    {
        var account = await accounts.GetAsync(User.AccountId(), cancellationToken);
        if (account is null)
        {
            // The account behind the session no longer exists.
            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            return Unauthorized();
        }

        IssueAntiforgeryToken();
        return AccountResponse.From(account);
    }

    /// <summary>
    /// Starts Google sign-in. The browser navigates here; it is not called with fetch. Google returns
    /// to the Client's <c>/google-callback</c> page, which passes the result to
    /// <c>/api/account/google-callback</c>; the Google handler checks it and redirects to
    /// <see cref="CompleteSignIn"/>.
    /// </summary>
    [HttpGet("sign-in")]
    public async Task<IActionResult> SignIn(string? returnUrl)
    {
        var schemes = HttpContext.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>();
        if (await schemes.GetSchemeAsync(GoogleDefaults.AuthenticationScheme) is null)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Google sign-in is not configured on this instance.",
                extensions: new Dictionary<string, object?> { ["code"] = "account.sign_in_unavailable" });
        }

        var complete = Url.Action(nameof(CompleteSignIn), new { returnUrl = LocalOrRoot(returnUrl) });
        return Challenge(new AuthenticationProperties { RedirectUri = complete }, GoogleDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Creates or updates the account from the Google result and starts the session. The Client's
    /// callback page reaches this through the Google handler's redirect, and navigates to the
    /// returned local URL.
    /// </summary>
    [HttpGet("sign-in/complete")]
    public async Task<ActionResult<SignInResponse>> CompleteSignIn(
        string? returnUrl, [FromServices] AccountService accounts, CancellationToken cancellationToken)
    {
        var external = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        var principal = external.Principal;
        var providerKey = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = principal?.FindFirstValue(ClaimTypes.Email);
        if (!external.Succeeded || providerKey is null || email is null)
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: SignInFailedTitle,
                extensions: new Dictionary<string, object?> { ["code"] = SignInFailedCode });
        }

        // Google is the only provider for now (D-008).
        var provider = GoogleDefaults.AuthenticationScheme;
        var emailVerified = bool.TryParse(principal!.FindFirstValue(AccountsServiceCollectionExtensions.EmailVerifiedClaim), out var verified)
            && verified;

        var account = await accounts.SignInAsync(new ExternalLogin(provider, providerKey, email, emailVerified), cancellationToken);

        var identity = new ClaimsIdentity(IdentityConstants.ApplicationScheme, ClaimTypes.NameIdentifier, ClaimTypes.Role);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, account.Id.Value.ToString()));
        identity.AddClaims(account.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, new ClaimsPrincipal(identity));

        return new SignInResponse(LocalOrRoot(returnUrl));
    }

    /// <summary>Ends the session. Protected against CSRF like every state-changing request (D-011).</summary>
    [HttpPost("sign-out")]
    public async Task<IActionResult> SignOutOfSession()
    {
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        Response.Cookies.Delete(XsrfCookie);
        return NoContent();
    }

    private string LocalOrRoot(string? returnUrl) => Url.IsLocalUrl(returnUrl) ? returnUrl! : "/";

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
