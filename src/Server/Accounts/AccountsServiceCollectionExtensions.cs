using FamilyGraph.Application.Accounts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Identity;

namespace Server.Accounts;

public static class AccountsServiceCollectionExtensions
{
    /// <summary>The claim the Google handler fills from the userinfo field of the same name.</summary>
    internal const string EmailVerifiedClaim = "email_verified";

    /// <summary>The API path the Client's <c>/google-callback</c> page passes Google's result to.</summary>
    internal const string GoogleCallbackPath = "/api/account/google-callback";

    /// <summary>
    /// Registers Google sign-in and the Identity session cookie (D-008, D-011). Google is registered
    /// only when <c>Authentication:Google:ClientId</c> is set, so the host starts without it.
    /// </summary>
    public static IServiceCollection AddAccounts(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(new AdministratorEmails(configuration.GetSection("Admin:Emails").Get<string[]>() ?? []));
        services.AddScoped<AccountService>();

        var authentication = services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ApplicationScheme, options =>
            {
                options.Cookie.Name = "FamilyGraph.Session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.SlidingExpiration = true;
                // The API answers with status codes; the Client decides where to send the user.
                options.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToLogin = context => WriteStatus(context.Response, StatusCodes.Status401Unauthorized),
                    OnRedirectToAccessDenied = context => WriteStatus(context.Response, StatusCodes.Status403Forbidden),
                };
            })
            // Holds the Google result for the few seconds between the callback and our own sign-in.
            .AddCookie(IdentityConstants.ExternalScheme, options =>
            {
                options.Cookie.Name = "FamilyGraph.External";
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
            });

        var google = GoogleSignInSettings.From(configuration);
        services.AddSingleton(google);
        if (google.Enabled)
        {
            authentication.AddOAuth<GoogleOptions, ClientCallbackGoogleHandler>(
                GoogleDefaults.AuthenticationScheme, GoogleDefaults.DisplayName, options => ConfigureGoogle(options, google));
        }

        services.AddAuthorization();
        return services;
    }

    private static void ConfigureGoogle(GoogleOptions options, GoogleSignInSettings google)
    {
        options.CallbackPath = GoogleCallbackPath;
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.ClientId = google.ClientId!;
        options.ClientSecret = google.ClientSecret
            ?? throw new InvalidOperationException("Authentication:Google:ClientSecret is not configured.");
        // An account keeps only the Google ID and email (D-018), so the profile scope is not requested.
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("email");
        options.ClaimActions.MapJsonKey(EmailVerifiedClaim, "email_verified");
        // The Client page calls the callback with HttpClient, so failures answer with a problem
        // instead of the default redirect or exception page.
        options.Events = new OAuthEvents
        {
            OnAccessDenied = context =>
            {
                context.HandleResponse();
                return WriteSignInFailed(context.HttpContext);
            },
            OnRemoteFailure = context =>
            {
                context.HandleResponse();
                return WriteSignInFailed(context.HttpContext);
            },
        };
    }

    private static Task WriteSignInFailed(HttpContext context) => Results.Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: AccountController.SignInFailedTitle,
        extensions: new Dictionary<string, object?> { ["code"] = AccountController.SignInFailedCode })
        .ExecuteAsync(context);

    private static Task WriteStatus(HttpResponse response, int statusCode)
    {
        response.StatusCode = statusCode;
        return Task.CompletedTask;
    }
}
