using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FamilyGraph.Business.People;
using Server.Accounts;
using Server.Administration;
using Server.IntegrationTests.Accounts;
using Server.People;

namespace Server.IntegrationTests.Administration;

/// <summary>The administrator's Users page and profile transfers (D-035, #94).</summary>
public class AdministrationEndpointTests(AccountTestFactory factory) : IClassFixture<AccountTestFactory>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_account_list_requires_a_session()
    {
        using var browser = factory.CreateBrowser();

        using var response = await browser.GetAsync("/api/admin/accounts", Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("AC", "AC-01.27")]
    public async Task A_user_who_is_not_an_administrator_cannot_list_accounts_or_transfer()
    {
        var (alice, token, aliceId) = await SignInAsync(UniqueEmail());
        var maya = await CreatePersonAsync(alice, token, "Maya");

        using var list = await alice.GetAsync("/api/admin/accounts", Cancellation);
        using var profiles = await alice.GetAsync($"/api/admin/accounts/{aliceId}/profiles", Cancellation);
        using var transfer = await SendAsync(alice, token, $"/api/admin/people/{maya}/transfer", new { toAccountId = aliceId });

        Assert.Equal(
            (HttpStatusCode.Forbidden, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden),
            (list.StatusCode, profiles.StatusCode, transfer.StatusCode));
        Assert.Equal("accounts.not_administrator", await CodeAsync(list));
        Assert.Equal("transfer.not_administrator", await CodeAsync(transfer));
    }

    [Fact]
    [Trait("AC", "AC-01.23")]
    public async Task Administrator_sees_each_account_with_its_flags_count_and_profiles()
    {
        var email = UniqueEmail();
        var (alice, token, aliceId) = await SignInAsync(email);
        var maya = await CreatePersonAsync(alice, token, "Maya");
        using var hidden = await SendAsync(alice, token, $"/api/people/{maya}/visibility", new { visibility = "Private" }, HttpMethod.Put);
        var (admin, _, _) = await SignInAsync(AccountTestFactory.AdministratorEmail);

        var list = await admin.GetFromJsonAsync<AccountListResponse>("/api/admin/accounts", Cancellation);
        var profiles = await admin.GetFromJsonAsync<ProfileListResponse>($"/api/admin/accounts/{aliceId}/profiles", Cancellation);

        var account = Assert.Single(list!.Accounts, account => account.Id == aliceId);
        Assert.Equal(new AccountSummaryResponse(aliceId, email, true, false, 1), account);
        Assert.Contains(list.Accounts, account => account.Email == AccountTestFactory.AdministratorEmail && account.Administrator);
        var profile = Assert.Single(profiles!.People);
        Assert.Equal((maya, "Maya", "Private"), (profile.Id, profile.FirstName, profile.Visibility));
    }

    [Fact]
    [Trait("AC", "AC-01.24")]
    [Trait("AC", "AC-01.26")]
    public async Task Administrator_transfers_one_profile_and_its_link_consent_stays()
    {
        var (alice, token, _) = await SignInAsync(UniqueEmail());
        var parent = await CreatePersonAsync(alice, token, "Hari");
        var child = await CreatePersonAsync(alice, token, "Maya");
        using var link = await SendAsync(alice, token, "/api/relationships", new { parentId = parent, childId = child, type = "Biological" });
        var (bob, _, bobId) = await SignInAsync(UniqueEmail());
        var (admin, adminToken, _) = await SignInAsync(AccountTestFactory.AdministratorEmail);
        var linkBefore = factory.People.Links.Values.Single(l => l.ChildId.Value == child);
        var approvals = (linkBefore.ParentApproval, linkBefore.ChildApproval, linkBefore.IsActive);

        using var response = await SendAsync(admin, adminToken, $"/api/admin/people/{child}/transfer", new { toAccountId = bobId });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var person = await bob.GetFromJsonAsync<JsonElement>($"/api/people/{child}", Cancellation);
        Assert.True(person.GetProperty("maintainedByYou").GetBoolean());
        var linkAfter = factory.People.Links.Values.Single(l => l.ChildId.Value == child);
        Assert.Equal(approvals, (linkAfter.ParentApproval, linkAfter.ChildApproval, linkAfter.IsActive));
        Assert.Contains(factory.People.AuditEvents, entry => entry.Event is PersonTransferred e && e.PersonId.Value == child);
    }

    [Fact]
    public async Task Transfer_to_the_current_maintainer_or_an_unknown_account_is_refused()
    {
        var (alice, token, aliceId) = await SignInAsync(UniqueEmail());
        var maya = await CreatePersonAsync(alice, token, "Maya");
        var (admin, adminToken, _) = await SignInAsync(AccountTestFactory.AdministratorEmail);

        using var same = await SendAsync(admin, adminToken, $"/api/admin/people/{maya}/transfer", new { toAccountId = aliceId });
        using var unknown = await SendAsync(admin, adminToken, $"/api/admin/people/{maya}/transfer", new { toAccountId = Guid.NewGuid() });
        using var missing = await SendAsync(admin, adminToken, $"/api/admin/people/{maya}/transfer", new { });

        Assert.Equal(
            (HttpStatusCode.Conflict, HttpStatusCode.NotFound, HttpStatusCode.BadRequest),
            (same.StatusCode, unknown.StatusCode, missing.StatusCode));
        Assert.Equal("transfer.same_maintainer", await CodeAsync(same));
        Assert.Equal("account.not_found", await CodeAsync(unknown));
    }

    [Fact]
    [Trait("AC", "AC-01.25")]
    public async Task Administrator_transfers_all_of_an_accounts_profiles()
    {
        var (alice, token, aliceId) = await SignInAsync(UniqueEmail());
        var maya = await CreatePersonAsync(alice, token, "Maya");
        var hari = await CreatePersonAsync(alice, token, "Hari");
        var (bob, _, bobId) = await SignInAsync(UniqueEmail());
        var (admin, adminToken, _) = await SignInAsync(AccountTestFactory.AdministratorEmail);

        using var response = await SendAsync(admin, adminToken, $"/api/admin/accounts/{aliceId}/transfer", new { toAccountId = bobId });
        using var same = await SendAsync(admin, adminToken, $"/api/admin/accounts/{bobId}/transfer", new { toAccountId = bobId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, (await response.Content.ReadFromJsonAsync<TransferredProfilesResponse>(Cancellation))!.Transferred);
        var maintained = await bob.GetFromJsonAsync<ProfileListResponse>("/api/people/maintained", Cancellation);
        Assert.Equal(new HashSet<Guid> { maya, hari }, maintained!.People.Select(person => person.Id).ToHashSet());
        Assert.Empty((await alice.GetFromJsonAsync<ProfileListResponse>("/api/people/maintained", Cancellation))!.People);
        Assert.Equal(HttpStatusCode.Conflict, same.StatusCode);
        Assert.Equal("transfer.same_account", await CodeAsync(same));
    }

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString();

    private async Task<(HttpClient Browser, string Token, Guid AccountId)> SignInAsync(string email)
    {
        var browser = factory.CreateBrowser();
        var token = await AccountTestFactory.SignInAsync(browser, email, Cancellation);
        var account = await browser.GetFromJsonAsync<AccountResponse>("/api/account", Cancellation);
        return (browser, token, account!.Id);
    }

    private static async Task<Guid> CreatePersonAsync(HttpClient browser, string token, string firstName)
    {
        using var response = await SendAsync(browser, token, "/api/people", new { firstName });
        return (await response.Content.ReadFromJsonAsync<PersonResponse>(Cancellation))!.Id;
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient browser, string token, string url, object body, HttpMethod? method = null)
    {
        using var request = new HttpRequestMessage(method ?? HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-XSRF-TOKEN", token);
        return await browser.SendAsync(request, Cancellation);
    }
}
