using FamilyGraph.Application.Accounts;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace Server.Accounts;

public static class AccountsServiceCollectionExtensions
{
    /// <summary>
    /// Registers Google ID token validation and the Identity session cookie (D-008, D-011). The host
    /// starts without <c>Authentication:Google:ClientId</c>; sign-in then answers 503.
    /// </summary>
    public static IServiceCollection AddAccounts(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(new AdministratorEmails(configuration.GetSection("Admin:Emails").Get<string[]>() ?? []));
        services.AddScoped<AccountService>();
        services.AddSingleton(GoogleSignInSettings.From(configuration));
        services.AddSingleton<IGoogleIdTokenValidator, GoogleIdTokenValidator>();

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
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
            });

        services.AddAuthorization();
        return services;
    }

    private static Task WriteStatus(HttpResponse response, int statusCode)
    {
        response.StatusCode = statusCode;
        return Task.CompletedTask;
    }
}
