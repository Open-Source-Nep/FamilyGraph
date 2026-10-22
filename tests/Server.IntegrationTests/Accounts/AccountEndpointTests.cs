using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Server.Accounts;

namespace Server.IntegrationTests.Accounts;

public class AccountEndpointTests : IClassFixture<AccountTestFactory>
{
    private readonly AccountTestFactory _factory;
    private readonly HttpClient _browser;

    public AccountEndpointTests(AccountTestFactory factory)
    {
        _factory = factory;
        _browser = factory.CreateBrowser();
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Account_requires_a_session_and_still_issues_the_antiforgery_token()
    {
        using var response = await _browser.GetAsync("/api/account", Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEmpty(AccountTestFactory.XsrfToken(response));
    }

    [Fact]
    public async Task Sign_in_reports_when_Google_is_not_configured()
    {
        using var unconfigured = _factory.WithWebHostBuilder(builder => builder.UseSetting("Authentication:Google:ClientId", ""));
        using var browser = unconfigured.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

        using var response = await SignInAsync(browser, "google-1", "maya@example.org");
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(Cancellation);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("account.sign_in_unavailable", problem!["code"].ToString());
    }

    [Fact]
    [Trait("AC", "AC-01.12")]
    public async Task First_sign_in_registers_the_account_and_starts_a_session()
    {
        using var signIn = await SignInAsync(_browser, Guid.NewGuid().ToString(), "maya@example.org");
        var account = await _browser.GetFromJsonAsync<AccountResponse>("/api/account", Cancellation);

        Assert.Equal(HttpStatusCode.NoContent, signIn.StatusCode);
        Assert.Equal("maya@example.org", account!.Email);
        Assert.True(account.EmailVerified);
        Assert.Equal(["User"], account.Roles);
        Assert.Contains(_factory.Store.Accounts.Values, stored => stored.Id.Value == account.Id);
    }

    [Fact]
    public async Task Signing_in_again_finds_the_same_account()
    {
        var subject = Guid.NewGuid().ToString();
        using var _ = await SignInAsync(_browser, subject, "maya@example.org");
        var first = await _browser.GetFromJsonAsync<AccountResponse>("/api/account", Cancellation);

        using var again = _factory.CreateBrowser();
        using var __ = await SignInAsync(again, subject, "maya@example.org");
        var second = await again.GetFromJsonAsync<AccountResponse>("/api/account", Cancellation);

        Assert.Equal(first!.Id, second!.Id);
    }

    [Fact]
    public async Task Configured_administrator_email_gets_the_administrator_role()
    {
        using var _ = await SignInAsync(_browser, Guid.NewGuid().ToString(), AccountTestFactory.AdministratorEmail);

        var account = await _browser.GetFromJsonAsync<AccountResponse>("/api/account", Cancellation);

        Assert.Equal(["User", "Administrator"], account!.Roles);
    }

    [Fact]
    public async Task An_invalid_ID_token_does_not_sign_in()
    {
        using var response = await AccountTestFactory.PostGoogleSignInAsync(_browser, "forged", "secret", Cancellation);

        await AssertSignInFailedAsync(response);
    }

    [Fact]
    public async Task An_ID_token_bound_to_another_nonce_secret_does_not_sign_in()
    {
        var stolen = AccountTestFactory.IdToken(Guid.NewGuid().ToString(), "maya@example.org", "victim-secret");

        using var response = await AccountTestFactory.PostGoogleSignInAsync(_browser, stolen, "attacker-secret", Cancellation);

        await AssertSignInFailedAsync(response);
    }

    [Fact]
    public async Task Sign_in_requires_the_antiforgery_token()
    {
        var idToken = AccountTestFactory.IdToken(Guid.NewGuid().ToString(), "maya@example.org", "secret");

        using var response = await _browser.PostAsJsonAsync(
            "/api/account/google-sign-in", new { idToken, nonceSecret = "secret" }, Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _browser.GetAsync("/api/account", Cancellation)).StatusCode);
    }

    [Fact]
    public async Task Sign_out_requires_the_antiforgery_token()
    {
        using var _ = await SignInAsync(_browser, Guid.NewGuid().ToString(), "maya@example.org");
        using var __ = await _browser.GetAsync("/api/account", Cancellation);

        using var response = await _browser.PostAsync("/api/account/sign-out", null, Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _browser.GetAsync("/api/account", Cancellation)).StatusCode);
    }

    [Fact]
    public async Task Sign_out_with_the_antiforgery_token_ends_the_session()
    {
        var token = await AccountTestFactory.SignInAsync(_browser, "maya@example.org", Cancellation);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/account/sign-out");
        request.Headers.Add("X-XSRF-TOKEN", token);
        using var response = await _browser.SendAsync(request, Cancellation);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _browser.GetAsync("/api/account", Cancellation)).StatusCode);
    }

    private static Task<HttpResponseMessage> SignInAsync(HttpClient browser, string subject, string email) =>
        AccountTestFactory.PostGoogleSignInAsync(
            browser, AccountTestFactory.IdToken(subject, email, "secret"), "secret", Cancellation);

    private async Task AssertSignInFailedAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("account.sign_in_failed", problem!["code"].ToString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await _browser.GetAsync("/api/account", Cancellation)).StatusCode);
    }
}
