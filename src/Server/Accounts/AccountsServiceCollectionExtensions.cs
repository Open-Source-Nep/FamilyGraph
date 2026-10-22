using FamilyGraph.Application.Accounts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace Server.Accounts;

public static class AccountsServiceCollectionExtensions
{
    /// <summary>The claim the Google handler fills from the userinfo field of the same name.</summary>
    internal const string EmailVerifiedClaim = "email_verified";

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
            authentication.AddGoogle(options =>
            {
                options.SignInScheme = IdentityConstants.ExternalScheme;
                options.ClientId = google.ClientId!;
                options.ClientSecret = google.ClientSecret
                    ?? throw new InvalidOperationException("Authentication:Google:ClientSecret is not configured.");
                // An account keeps only the Google ID and email (D-018), so the profile scope is not requested.
                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("email");
                options.ClaimActions.MapJsonKey(EmailVerifiedClaim, "email_verified");
            });
        }

        services.AddAuthorization();
        return services;
    }

    private static Task WriteStatus(HttpResponse response, int statusCode)
    {
        response.StatusCode = statusCode;
        return Task.CompletedTask;
    }
}
