using System.Buffers.Text;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using FamilyGraph.Application.Accounts;
using FamilyGraph.Application.Export;
using FamilyGraph.Application.Graph;
using FamilyGraph.Application.Handovers;
using FamilyGraph.Application.Import;
using FamilyGraph.Application.People;
using FamilyGraph.Application.Persistence;
using FamilyGraph.Application.Relationships;
using FamilyGraph.Business.People;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Server.Accounts;
using Server.IntegrationTests.Administration;
using Server.IntegrationTests.Export;
using Server.IntegrationTests.Graph;
using Server.IntegrationTests.Handovers;
using Server.IntegrationTests.Import;
using Server.IntegrationTests.People;

namespace Server.IntegrationTests.Accounts;

/// <summary>
/// Runs the Server with in-memory account, people, link and handover stores, graph and export queries, the import store and a fake Google ID token
/// validator, so the endpoints are tested without Google or a database. A fake ID token is
/// "subject|email|verified|nonce" (see <see cref="IdToken"/>).
/// </summary>
public sealed class AccountTestFactory : WebApplicationFactory<Program>
{
    public const string AdministratorEmail = "admin@example.org";

    public AccountTestFactory()
    {
        Graph = new InMemoryGraph(People);
        Export = new InMemoryExport(Store, People);
        Import = new InMemoryImport(Store, People);
    }

    public InMemoryAccountStore Store { get; } = new();

    public InMemoryPeople People { get; } = new();

    public InMemoryGraph Graph { get; }

    public InMemoryHandovers Handovers { get; } = new();

    public InMemoryExport Export { get; }

    public InMemoryImport Import { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Admin:Emails:0", AdministratorEmail);
        builder.UseSetting("Authentication:Google:ClientId", "test-client");
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IAccountStore>(Store);
            services.AddSingleton<IAccountQueries>(new InMemoryAccountQueries(Store, People));
            services.AddSingleton<IPersonStore>(People);
            services.AddSingleton<IPersonQueries>(People);
            services.AddSingleton<IProfileImageStore>(People);
            services.AddSingleton<IParentChildLinkStore>(People);
            services.AddSingleton<IRelationshipQueries>(People);
            services.AddSingleton<IMarriageStore>(People);
            services.AddSingleton<IMarriageQueries>(People);
            services.AddSingleton<IAuditLog>(People);
            services.AddSingleton<IUnitOfWork>(People);
            services.AddSingleton<IGraphQueries>(Graph);
            services.AddSingleton<IHandoverStore>(Handovers);
            services.AddSingleton<IExportQueries>(Export);
            services.AddSingleton<IImportStore>(Import);
            services.AddSingleton<IGoogleIdTokenValidator, FakeIdTokenValidator>();
        });
    }

    public HttpClient CreateBrowser() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
    });

    /// <summary>A fake ID token for a Google user, bound to <paramref name="nonceSecret"/>.</summary>
    public static string IdToken(string subject, string email, string nonceSecret, bool verified = true) =>
        $"{subject}|{email}|{verified}|{Nonce(nonceSecret)}";

    /// <summary>The nonce the Client sends to Google: base64url of the SHA-256 of the secret.</summary>
    public static string Nonce(string nonceSecret) =>
        Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(nonceSecret)));

    /// <summary>
    /// Posts a Google sign-in the way the Client does: loads the account first for the antiforgery
    /// token, then sends the ID token and nonce secret.
    /// </summary>
    public static async Task<HttpResponseMessage> PostGoogleSignInAsync(
        HttpClient browser, string idToken, string nonceSecret, CancellationToken cancellationToken)
    {
        using var account = await browser.GetAsync("/api/account", cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/account/google-sign-in")
        {
            Content = JsonContent.Create(new { idToken, nonceSecret }),
        };
        request.Headers.Add("X-XSRF-TOKEN", XsrfToken(account));
        return await browser.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Signs a new account in through a fake Google ID token and returns the antiforgery token for
    /// its state-changing requests.
    /// </summary>
    public static async Task<string> SignInAsync(HttpClient browser, string email, CancellationToken cancellationToken)
    {
        using var _ = await PostGoogleSignInAsync(
            browser, IdToken(Guid.NewGuid().ToString(), email, "secret"), "secret", cancellationToken);
        using var account = await browser.GetAsync("/api/account", cancellationToken);
        return XsrfToken(account);
    }

    public static string XsrfToken(HttpResponseMessage response)
    {
        var cookie = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
        return Uri.UnescapeDataString(cookie["XSRF-TOKEN=".Length..cookie.IndexOf(';')]);
    }

    /// <summary>Accepts "subject|email|verified|nonce"; anything else is not a valid token.</summary>
    private sealed class FakeIdTokenValidator : IGoogleIdTokenValidator
    {
        public Task<GoogleIdToken?> ValidateAsync(string idToken)
        {
            var parts = idToken.Split('|');
            return Task.FromResult(parts.Length == 4 ? new GoogleIdToken(parts[0], parts[1], bool.Parse(parts[2]), parts[3]) : null);
        }
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
