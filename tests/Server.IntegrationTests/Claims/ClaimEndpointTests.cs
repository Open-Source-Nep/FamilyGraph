using System.Net;
using System.Net.Http.Json;
using Server.Claims;
using Server.IntegrationTests.Accounts;
using Server.People;

namespace Server.IntegrationTests.Claims;

public class ClaimEndpointTests(AccountTestFactory factory) : IClassFixture<AccountTestFactory>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("GET", "/api/people/0199f6a2-0000-7000-8000-000000000001/claims")]
    [InlineData("POST", "/api/people/0199f6a2-0000-7000-8000-000000000001/claims")]
    [InlineData("GET", "/api/claims/to-decide")]
    [InlineData("GET", "/api/claims/mine")]
    [InlineData("POST", "/api/claims/0199f6a2-0000-7000-8000-000000000001/approve")]
    [InlineData("POST", "/api/claims/0199f6a2-0000-7000-8000-000000000001/review")]
    [InlineData("GET", "/api/claims/to-respond")]
    [InlineData("GET", "/api/admin/claims")]
    [InlineData("POST", "/api/admin/claims/0199f6a2-0000-7000-8000-000000000001/approve")]
    public async Task Claims_require_a_session(string method, string url)
    {
        using var browser = factory.CreateBrowser();

        using var response = await browser.SendAsync(new HttpRequestMessage(new HttpMethod(method), url), Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("AC", "AC-04.1")]
    [Trait("AC", "AC-04.3")]
    public async Task A_claimant_claims_and_the_maintainer_approves()
    {
        var (alice, aliceToken) = await SignInAsync("alice-claim@example.org");
        var personId = await CreatePersonAsync(alice, aliceToken, "Maya");
        var (bob, bobToken) = await SignInAsync("bob-claim@example.org");

        using var submitted = await SendAsync(bob, bobToken, HttpMethod.Post, $"/api/people/{personId}/claims", new { evidence = "I am Maya." });
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        var claim = (await submitted.Content.ReadFromJsonAsync<ClaimResponse>(Cancellation))!;
        Assert.Equal(("Open", "I am Maya.", personId), (claim.State, claim.Evidence, claim.Person.Id));

        using var toDecide = await SendAsync(alice, aliceToken, HttpMethod.Get, "/api/claims/to-decide");
        var listed = Assert.Single((await toDecide.Content.ReadFromJsonAsync<ClaimListResponse>(Cancellation))!.Claims);
        Assert.Equal((claim.Id, "I am Maya."), (listed.Id, listed.Evidence));

        using var approved = await SendAsync(alice, aliceToken, HttpMethod.Post, $"/api/claims/{claim.Id}/approve");
        Assert.Equal(HttpStatusCode.NoContent, approved.StatusCode);

        using var person = await SendAsync(bob, bobToken, HttpMethod.Get, $"/api/people/{personId}");
        Assert.True((await person.Content.ReadFromJsonAsync<PersonResponse>(Cancellation))!.MaintainedByYou);
        using var status = await SendAsync(bob, bobToken, HttpMethod.Get, $"/api/people/{personId}/claims");
        var claimStatus = (await status.Content.ReadFromJsonAsync<ProfileClaimStatusResponse>(Cancellation))!;
        Assert.True(claimStatus.ClaimedByYou);
        Assert.True(claimStatus.Claimed);

        using var again = await SendAsync(alice, aliceToken, HttpMethod.Post, $"/api/claims/{claim.Id}/reject");
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("claim.closed", await CodeAsync(again));

        using var handover = await SendAsync(bob, bobToken, HttpMethod.Post, $"/api/people/{personId}/handover");
        Assert.Equal(HttpStatusCode.Conflict, handover.StatusCode);
        Assert.Equal("person.claimed", await CodeAsync(handover));
    }

    [Fact]
    [Trait("AC", "AC-04.5")]
    [Trait("AC", "AC-04.1")]
    public async Task Others_cannot_see_or_decide_a_claim()
    {
        var (alice, aliceToken) = await SignInAsync("alice-claim2@example.org");
        var personId = await CreatePersonAsync(alice, aliceToken, "Maya");
        var (bob, bobToken) = await SignInAsync("bob-claim2@example.org");
        var (carol, carolToken) = await SignInAsync("carol-claim2@example.org");
        using var submitted = await SendAsync(bob, bobToken, HttpMethod.Post, $"/api/people/{personId}/claims", new { evidence = "secret note" });
        var claim = (await submitted.Content.ReadFromJsonAsync<ClaimResponse>(Cancellation))!;

        using var status = await SendAsync(carol, carolToken, HttpMethod.Get, $"/api/people/{personId}/claims");
        var body = await status.Content.ReadAsStringAsync(Cancellation);
        Assert.DoesNotContain("secret note", body, StringComparison.Ordinal);
        var carolStatus = (await status.Content.ReadFromJsonAsync<ProfileClaimStatusResponse>(Cancellation))!;
        Assert.Null(carolStatus.Claimed);
        Assert.Empty(carolStatus.ToDecide);

        using var approve = await SendAsync(carol, carolToken, HttpMethod.Post, $"/api/claims/{claim.Id}/approve");
        Assert.Equal(HttpStatusCode.Forbidden, approve.StatusCode);
        Assert.Equal("person.not_maintainer", await CodeAsync(approve));
        using var withdraw = await SendAsync(alice, aliceToken, HttpMethod.Post, $"/api/claims/{claim.Id}/withdraw");
        Assert.Equal(HttpStatusCode.Forbidden, withdraw.StatusCode);
        Assert.Equal("claim.not_claimant", await CodeAsync(withdraw));
    }

    [Fact]
    [Trait("AC", "AC-04.11")]
    [Trait("AC", "AC-04.12")]
    public async Task This_is_me_marks_a_profile_and_then_no_other_claim_is_accepted()
    {
        var (dina, dinaToken) = await SignInAsync("dina-claim@example.org");
        var mine = await CreatePersonAsync(dina, dinaToken, "Dina");
        var (alice, aliceToken) = await SignInAsync("alice-claim3@example.org");
        var other = await CreatePersonAsync(alice, aliceToken, "Maya");

        using var marked = await SendAsync(dina, dinaToken, HttpMethod.Post, $"/api/people/{mine}/claim-as-self");
        Assert.Equal(HttpStatusCode.NoContent, marked.StatusCode);

        using var second = await SendAsync(dina, dinaToken, HttpMethod.Post, $"/api/people/{other}/claims", new { });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("claim.account_has_claim", await CodeAsync(second));
    }

    [Fact]
    [Trait("AC", "AC-04.14")]
    public async Task A_note_over_2000_characters_is_refused()
    {
        var (alice, aliceToken) = await SignInAsync("alice-claim4@example.org");
        var personId = await CreatePersonAsync(alice, aliceToken, "Maya");
        var (erin, erinToken) = await SignInAsync("erin-claim@example.org");

        using var response = await SendAsync(erin, erinToken, HttpMethod.Post, $"/api/people/{personId}/claims", new { evidence = new string('a', 2001) });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("claim.evidence_too_long", await CodeAsync(response));
    }

    [Fact]
    [Trait("AC", "AC-04.2")]
    [Trait("AC", "AC-04.5")]
    [Trait("AC", "AC-04.8")]
    public async Task A_rejected_claim_is_appealed_and_an_administrator_decides_it()
    {
        var (alice, aliceToken) = await SignInAsync("alice-claim5@example.org");
        var personId = await CreatePersonAsync(alice, aliceToken, "Maya");
        var (bob, bobToken) = await SignInAsync("bob-claim5@example.org");
        var (admin, adminToken) = await SignInAsync(AccountTestFactory.AdministratorEmail);
        using var submitted = await SendAsync(bob, bobToken, HttpMethod.Post, $"/api/people/{personId}/claims", new { evidence = "I am Maya." });
        var claim = (await submitted.Content.ReadFromJsonAsync<ClaimResponse>(Cancellation))!;
        using var rejected = await SendAsync(alice, aliceToken, HttpMethod.Post, $"/api/claims/{claim.Id}/reject");

        using var appeal = await SendAsync(bob, bobToken, HttpMethod.Post, $"/api/claims/{claim.Id}/review");
        Assert.Equal(HttpStatusCode.NoContent, appeal.StatusCode);

        using var forbidden = await SendAsync(alice, aliceToken, HttpMethod.Get, "/api/admin/claims");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("claim.not_administrator", await CodeAsync(forbidden));
        using var list = await SendAsync(admin, adminToken, HttpMethod.Get, "/api/admin/claims");
        var listed = (await list.Content.ReadFromJsonAsync<ClaimListResponse>(Cancellation))!.Claims.Single(c => c.Id == claim.Id);
        Assert.Equal(("I am Maya.", true, true), (listed.Evidence, listed.Appealed, listed.WithAdministrators));

        using var noReason = await SendAsync(admin, adminToken, HttpMethod.Post, $"/api/admin/claims/{claim.Id}/reject", new { });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noReason.StatusCode);
        Assert.Equal("claim.rationale_required", await CodeAsync(noReason));
        using var decided = await SendAsync(admin, adminToken, HttpMethod.Post, $"/api/admin/claims/{claim.Id}/reject", new { rationale = "No one confirmed it." });
        Assert.Equal(HttpStatusCode.NoContent, decided.StatusCode);

        using var status = await SendAsync(bob, bobToken, HttpMethod.Get, $"/api/people/{personId}/claims");
        var body = await status.Content.ReadAsStringAsync(Cancellation);
        Assert.DoesNotContain("No one confirmed it.", body, StringComparison.Ordinal);
        var bobStatus = (await status.Content.ReadFromJsonAsync<ProfileClaimStatusResponse>(Cancellation))!;
        Assert.True(bobStatus.YourClaim!.DecidedByAdministrator);
        Assert.NotNull(bobStatus.ClaimAgainFrom);
        using var again = await SendAsync(bob, bobToken, HttpMethod.Post, $"/api/people/{personId}/claims", new { });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("claim.waiting_period", await CodeAsync(again));
    }

    [Fact]
    [Trait("AC", "AC-04.9")]
    public async Task The_claimed_person_replies_and_an_administrator_asks_the_claimant()
    {
        var (alice, aliceToken) = await SignInAsync("alice-claim6@example.org");
        var personId = await CreatePersonAsync(alice, aliceToken, "Maya");
        using var self = await SendAsync(alice, aliceToken, HttpMethod.Post, $"/api/people/{personId}/claim-as-self");
        var (bob, bobToken) = await SignInAsync("bob-claim6@example.org");
        var (admin, adminToken) = await SignInAsync(AccountTestFactory.AdministratorEmail);
        using var submitted = await SendAsync(bob, bobToken, HttpMethod.Post, $"/api/people/{personId}/claims", new { evidence = "secret note" });
        var claim = (await submitted.Content.ReadFromJsonAsync<ClaimResponse>(Cancellation))!;

        using var toRespond = await SendAsync(alice, aliceToken, HttpMethod.Get, "/api/claims/to-respond");
        var body = await toRespond.Content.ReadAsStringAsync(Cancellation);
        Assert.DoesNotContain("secret note", body, StringComparison.Ordinal);
        using var reply = await SendAsync(alice, aliceToken, HttpMethod.Put, $"/api/claims/{claim.Id}/response", new { response = "Not me." });
        Assert.Equal(HttpStatusCode.NoContent, reply.StatusCode);
        using var ask = await SendAsync(admin, adminToken, HttpMethod.Post, $"/api/admin/claims/{claim.Id}/ask", new { question = "Who can confirm it?" });
        Assert.Equal(HttpStatusCode.NoContent, ask.StatusCode);
        using var change = await SendAsync(bob, bobToken, HttpMethod.Put, $"/api/claims/{claim.Id}/evidence", new { evidence = "My aunt Sita." });
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        using var list = await SendAsync(admin, adminToken, HttpMethod.Get, "/api/admin/claims");
        var listed = (await list.Content.ReadFromJsonAsync<ClaimListResponse>(Cancellation))!.Claims.Single(c => c.Id == claim.Id);
        Assert.Equal(("My aunt Sita.", "Not me.", "Who can confirm it?", (bool?)true), (listed.Evidence, listed.MaintainerResponse, listed.AdministratorQuestion, listed.ProfileClaimed));
        using var approved = await SendAsync(admin, adminToken, HttpMethod.Post, $"/api/admin/claims/{claim.Id}/approve", new { rationale = "Met in person." });
        Assert.Equal(HttpStatusCode.NoContent, approved.StatusCode);
        using var person = await SendAsync(bob, bobToken, HttpMethod.Get, $"/api/people/{personId}");
        Assert.True((await person.Content.ReadFromJsonAsync<PersonResponse>(Cancellation))!.MaintainedByYou);
    }

    private async Task<(HttpClient Browser, string Token)> SignInAsync(string email)
    {
        var browser = factory.CreateBrowser();
        return (browser, await AccountTestFactory.SignInAsync(browser, email, Cancellation));
    }

    private static async Task<Guid> CreatePersonAsync(HttpClient browser, string token, string firstName)
    {
        using var response = await SendAsync(browser, token, HttpMethod.Post, "/api/people", new { firstName });
        return (await response.Content.ReadFromJsonAsync<PersonResponse>(Cancellation))!.Id;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient browser, string token, HttpMethod method, string url, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = body is null ? null : JsonContent.Create(body) };
        request.Headers.Add("X-XSRF-TOKEN", token);
        return await browser.SendAsync(request, Cancellation);
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(Cancellation))!["code"].ToString();
}
