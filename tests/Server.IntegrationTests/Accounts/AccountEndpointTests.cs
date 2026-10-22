using System.Net;
using System.Net.Http.Json;
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
    public async Task Account_requires_a_session()
    {
        using var response = await _browser.GetAsync("/api/account", Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Sign_in_reports_when_Google_is_not_configured()
    {
        using var response = await _browser.GetAsync("/api/account/sign-in", Cancellation);
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(Cancellation);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("account.sign_in_unavailable", problem!["code"].ToString());
    }

    [Fact]
    [Trait("AC", "AC-01.12")]
    public async Task First_sign_in_registers_the_account_and_starts_a_session()
    {
        var key = Guid.NewGuid().ToString();

        using var callback = await CallbackAsync(key, "maya@example.org", returnUrl: "/people");
        var account = await _browser.GetFromJsonAsync<AccountResponse>("/api/account", Cancellation);

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("/people", callback.Headers.Location!.OriginalString);
        Assert.Equal("maya@example.org", account!.Email);
        Assert.True(account.EmailVerified);
        Assert.Equal(["User"], account.Roles);
        Assert.Contains(_factory.Store.Accounts.Values, stored => stored.Id.Value == account.Id);
    }

    [Fact]
    public async Task Configured_administrator_email_gets_the_administrator_role()
    {
        using var _ = await CallbackAsync(Guid.NewGuid().ToString(), AccountTestFactory.AdministratorEmail);

        var account = await _browser.GetFromJsonAsync<AccountResponse>("/api/account", Cancellation);

        Assert.Equal(["User", "Administrator"], account!.Roles);
    }

    [Theory]
    [InlineData("https://evil.example.org/")]
    [InlineData("//evil.example.org/")]
    public async Task Sign_in_never_redirects_off_site(string returnUrl)
    {
        using var callback = await CallbackAsync(Guid.NewGuid().ToString(), "maya@example.org", returnUrl);

        Assert.Equal("/", callback.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Callback_without_a_Google_result_goes_back_to_the_sign_in_page()
    {
        using var response = await _browser.GetAsync("/api/account/sign-in/callback", Cancellation);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/sign-in?failed=true", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Sign_out_requires_the_antiforgery_token()
    {
        using var _ = await CallbackAsync(Guid.NewGuid().ToString(), "maya@example.org");
        using var __ = await _browser.GetAsync("/api/account", Cancellation);

        using var response = await _browser.PostAsync("/api/account/sign-out", null, Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _browser.GetAsync("/api/account", Cancellation)).StatusCode);
    }

    [Fact]
    public async Task Sign_out_with_the_antiforgery_token_ends_the_session()
    {
        using var _ = await CallbackAsync(Guid.NewGuid().ToString(), "maya@example.org");
        using var account = await _browser.GetAsync("/api/account", Cancellation);
        var token = XsrfToken(account);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/account/sign-out");
        request.Headers.Add("X-XSRF-TOKEN", token);
        using var response = await _browser.SendAsync(request, Cancellation);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _browser.GetAsync("/api/account", Cancellation)).StatusCode);
    }

    private async Task<HttpResponseMessage> CallbackAsync(string key, string email, string returnUrl = "/")
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, "/api/account/sign-in/callback?returnUrl=" + Uri.EscapeDataString(returnUrl));
        request.Headers.Add(AccountTestFactory.LoginHeader, $"{key}|{email}|true");
        return await _browser.SendAsync(request, Cancellation);
    }

    private static string XsrfToken(HttpResponseMessage response)
    {
        var cookie = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
        return Uri.UnescapeDataString(cookie["XSRF-TOKEN=".Length..cookie.IndexOf(';')]);
    }
}
