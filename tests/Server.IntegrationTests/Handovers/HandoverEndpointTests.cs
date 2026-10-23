using System.Net;
using System.Net.Http.Json;
using Server.Handovers;
using Server.IntegrationTests.Accounts;
using Server.People;

namespace Server.IntegrationTests.Handovers;

public class HandoverEndpointTests(AccountTestFactory factory) : IClassFixture<AccountTestFactory>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("GET", "/api/people/0199f6a2-0000-7000-8000-000000000001/handover")]
    [InlineData("POST", "/api/handovers/preview")]
    [InlineData("POST", "/api/handovers/accept")]
    public async Task Handovers_require_a_session(string method, string url)
    {
        using var browser = factory.CreateBrowser();

        using var response = await browser.SendAsync(new HttpRequestMessage(new HttpMethod(method), url), Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("AC", "AC-01.13")]
    [Trait("AC", "AC-01.14")]
    public async Task Maintainer_creates_a_link_and_the_receiver_previews_and_accepts_it()
    {
        var (alice, aliceToken) = await SignInAsync("alice-handover@example.org");
        var personId = await CreatePersonAsync(alice, aliceToken, "Maya");

        using var offered = await SendAsync(alice, aliceToken, HttpMethod.Post, $"/api/people/{personId}/handover");
        Assert.Equal(HttpStatusCode.Created, offered.StatusCode);
        var created = (await offered.Content.ReadFromJsonAsync<CreatedHandoverResponse>(Cancellation))!;
        Assert.False(string.IsNullOrEmpty(created.Token));

        using var open = await SendAsync(alice, aliceToken, HttpMethod.Get, $"/api/people/{personId}/handover");
        Assert.Equal(created.ExpiresAt, (await open.Content.ReadFromJsonAsync<OpenHandoverResponse>(Cancellation))!.ExpiresAt);

        var (bob, bobToken) = await SignInAsync("bob-handover@example.org");
        using var previewed = await SendAsync(bob, bobToken, HttpMethod.Post, "/api/handovers/preview", new { token = created.Token });
        var preview = (await previewed.Content.ReadFromJsonAsync<HandoverPreviewResponse>(Cancellation))!;
        Assert.Equal((personId, "Maya", false), (preview.Person.Id, preview.Person.FirstName, preview.Person.MaintainedByYou));

        using var accepted = await SendAsync(bob, bobToken, HttpMethod.Post, "/api/handovers/accept", new { token = created.Token });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(personId, (await accepted.Content.ReadFromJsonAsync<AcceptedHandoverResponse>(Cancellation))!.PersonId);

        using var person = await SendAsync(bob, bobToken, HttpMethod.Get, $"/api/people/{personId}");
        Assert.True((await person.Content.ReadFromJsonAsync<PersonResponse>(Cancellation))!.MaintainedByYou);
        using var rename = await SendAsync(alice, aliceToken, HttpMethod.Put, $"/api/people/{personId}/name", new { firstName = "Mira" });
        Assert.Equal(HttpStatusCode.Forbidden, rename.StatusCode);

        using var again = await SendAsync(bob, bobToken, HttpMethod.Post, "/api/handovers/accept", new { token = created.Token });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("handover.closed", await CodeAsync(again));
    }

    [Fact]
    [Trait("AC", "AC-01.13")]
    public async Task Only_the_maintainer_creates_sees_or_cancels_a_link()
    {
        var (alice, aliceToken) = await SignInAsync("alice-handover@example.org");
        var personId = await CreatePersonAsync(alice, aliceToken, "Maya");
        var (bob, bobToken) = await SignInAsync("bob-handover@example.org");

        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Get, HttpMethod.Delete })
        {
            using var response = await SendAsync(bob, bobToken, method, $"/api/people/{personId}/handover");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("person.not_maintainer", await CodeAsync(response));
        }
    }

    [Fact]
    [Trait("AC", "AC-01.16")]
    public async Task A_cancelled_link_cannot_be_accepted()
    {
        var (alice, aliceToken) = await SignInAsync("alice-handover@example.org");
        var personId = await CreatePersonAsync(alice, aliceToken, "Maya");
        using var offered = await SendAsync(alice, aliceToken, HttpMethod.Post, $"/api/people/{personId}/handover");
        var created = (await offered.Content.ReadFromJsonAsync<CreatedHandoverResponse>(Cancellation))!;

        using var cancelled = await SendAsync(alice, aliceToken, HttpMethod.Delete, $"/api/people/{personId}/handover");
        using var open = await SendAsync(alice, aliceToken, HttpMethod.Get, $"/api/people/{personId}/handover");
        var (bob, bobToken) = await SignInAsync("bob-handover@example.org");
        using var accepted = await SendAsync(bob, bobToken, HttpMethod.Post, "/api/handovers/accept", new { token = created.Token });

        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, open.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, accepted.StatusCode);
        Assert.Equal("handover.closed", await CodeAsync(accepted));
    }

    [Fact]
    public async Task Unknown_and_missing_tokens_are_refused()
    {
        var (bob, bobToken) = await SignInAsync("bob-handover@example.org");

        using var unknown = await SendAsync(bob, bobToken, HttpMethod.Post, "/api/handovers/accept", new { token = "unknown" });
        using var missing = await SendAsync(bob, bobToken, HttpMethod.Post, "/api/handovers/preview", new { });

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("handover.not_found", await CodeAsync(unknown));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    [Fact]
    [Trait("AC", "AC-01.14")]
    public async Task The_maintainer_cannot_preview_their_own_link()
    {
        var (alice, aliceToken) = await SignInAsync("alice-handover@example.org");
        var personId = await CreatePersonAsync(alice, aliceToken, "Maya");
        using var offered = await SendAsync(alice, aliceToken, HttpMethod.Post, $"/api/people/{personId}/handover");
        var created = (await offered.Content.ReadFromJsonAsync<CreatedHandoverResponse>(Cancellation))!;

        using var previewed = await SendAsync(alice, aliceToken, HttpMethod.Post, "/api/handovers/preview", new { token = created.Token });

        Assert.Equal(HttpStatusCode.Conflict, previewed.StatusCode);
        Assert.Equal("handover.own_profile", await CodeAsync(previewed));
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
