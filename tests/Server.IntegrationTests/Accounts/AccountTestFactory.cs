using System.Security.Claims;
using System.Text.Encodings.Web;
using FamilyGraph.Application.Accounts;
using FamilyGraph.Business.People;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Server.IntegrationTests.Accounts;

/// <summary>
/// Runs the Server with an in-memory account store and a fake external sign-in, so the account
/// endpoints are tested without Google or a database. A request carrying <see cref="LoginHeader"/>
/// arrives at the callback as if Google had just signed in that user.
/// </summary>
public sealed class AccountTestFactory : WebApplicationFactory<Program>
{
    public const string LoginHeader = "X-Test-Google-Login";
    public const string AdministratorEmail = "admin@example.org";

    public InMemoryAccountStore Store { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Admin:Emails:0", AdministratorEmail);
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IAccountStore>(Store);
            services.Configure<AuthenticationOptions>(options =>
                options.SchemeMap[IdentityConstants.ExternalScheme].HandlerType = typeof(FakeExternalHandler));
        });
    }

    public HttpClient CreateBrowser() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
    });

    /// <summary>Header value "providerKey|email|verified".</summary>
    private sealed class FakeExternalHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder), IAuthenticationSignOutHandler
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(LoginHeader, out var value))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var parts = value.ToString().Split('|');
            var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, parts[0]),
                    new Claim(ClaimTypes.Email, parts[1]),
                    new Claim("email_verified", parts[2]),
                ],
                Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }

        public Task SignOutAsync(AuthenticationProperties? properties) => Task.CompletedTask;
    }
}

public sealed class InMemoryAccountStore : IAccountStore
{
    private readonly Dictionary<(string Provider, string Key), AccountId> _logins = [];

    public Dictionary<AccountId, AccountView> Accounts { get; } = [];

    public Task<AccountView?> FindAsync(AccountId id, CancellationToken cancellationToken) =>
        Task.FromResult(Accounts.GetValueOrDefault(id));

    public Task<AccountView?> FindByLoginAsync(string provider, string providerKey, CancellationToken cancellationToken) =>
        Task.FromResult(_logins.TryGetValue((provider, providerKey), out var id) ? Accounts[id] : null);

    public Task<AccountView> CreateAsync(ExternalLogin login, bool isAdministrator, CancellationToken cancellationToken)
    {
        var account = new AccountView(new AccountId(Guid.NewGuid()), login.Email, login.EmailVerified, isAdministrator);
        Accounts[account.Id] = account;
        _logins[(login.Provider, login.ProviderKey)] = account.Id;
        return Task.FromResult(account);
    }

    public Task<AccountView> UpdateAsync(
        AccountId id, string email, bool emailVerified, bool isAdministrator, CancellationToken cancellationToken)
    {
        var account = Accounts[id] with { Email = email, EmailVerified = emailVerified, IsAdministrator = isAdministrator };
        Accounts[id] = account;
        return Task.FromResult(account);
    }
}
