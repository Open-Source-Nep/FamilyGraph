using System.Net;
using System.Net.Http.Json;
using FamilyGraph.Business.Relationships;
using Server.IntegrationTests.Accounts;
using Server.People;
using Server.Relationships;

namespace Server.IntegrationTests.Relationships;

public class RelationshipEndpointTests(AccountTestFactory factory) : IClassFixture<AccountTestFactory>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("POST", "/api/relationships")]
    [InlineData("GET", "/api/people/0199f6a2-0000-7000-8000-000000000001/relatives")]
    public async Task Relationships_require_a_session(string method, string url)
    {
        using var browser = factory.CreateBrowser();

        using var response = await browser.SendAsync(new HttpRequestMessage(new HttpMethod(method), url), Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [Trait("AC", "AC-02.1")]
    [InlineData("Biological")]
    [InlineData("Adoptive")]
    public async Task Maintainer_of_both_creates_an_active_link_of_either_type(string type)
    {
        var (browser, token) = await SignInAsync("maya@example.org");
        var parent = await CreatePersonAsync(browser, token, "Hari");
        var child = await CreatePersonAsync(browser, token, "Maya");

        using var response = await CreateLinkAsync(browser, token, parent, child, type);
        var link = await response.Content.ReadFromJsonAsync<CreatedLinkResponse>(Cancellation);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal((parent, child, type, true), (link!.ParentId, link.ChildId, link.Type, link.IsActive));
        var created = Assert.IsType<LinkCreated>(factory.People.AuditEvents[^1].Event);
        Assert.Equal(new LinkId(link.Id), created.LinkId);
    }

    [Fact]
    [Trait("AC", "AC-02.1")]
    public async Task Parents_and_children_show_the_type_of_each_active_link()
    {
        var (browser, token) = await SignInAsync("maya@example.org");
        var mother = await CreatePersonAsync(browser, token, "Sita");
        var father = await CreatePersonAsync(browser, token, "Hari");
        var child = await CreatePersonAsync(browser, token, "Maya");
        (await CreateLinkAsync(browser, token, mother, child, "Biological")).Dispose();
        (await CreateLinkAsync(browser, token, father, child, "Adoptive")).Dispose();

        var ofChild = await browser.GetFromJsonAsync<DirectRelativesResponse>($"/api/people/{child}/relatives", Cancellation);
        var ofFather = await browser.GetFromJsonAsync<DirectRelativesResponse>($"/api/people/{father}/relatives", Cancellation);

        Assert.Equal(
            [("Hari", "Adoptive"), ("Sita", "Biological")],
            ofChild!.Parents.Select(parent => (parent.FirstName, parent.Type)));
        Assert.Empty(ofChild.Children);
        var onlyChild = Assert.Single(ofFather!.Children);
        Assert.Equal(("Maya", "Adoptive", (int?)null), (onlyChild.FirstName, onlyChild.Type, onlyChild.BirthOrder));
    }

    [Fact]
    public async Task Link_to_a_profile_someone_else_maintains_is_pending_and_not_listed()
    {
        var (other, otherToken) = await SignInAsync("gita@example.org");
        var parent = await CreatePersonAsync(other, otherToken, "Gita");
        var (browser, token) = await SignInAsync("maya@example.org");
        var child = await CreatePersonAsync(browser, token, "Maya");

        using var response = await CreateLinkAsync(browser, token, parent, child, "Biological");
        var link = await response.Content.ReadFromJsonAsync<CreatedLinkResponse>(Cancellation);
        var relatives = await browser.GetFromJsonAsync<DirectRelativesResponse>($"/api/people/{child}/relatives", Cancellation);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.False(link!.IsActive);
        Assert.Empty(relatives!.Parents);
    }

    [Fact]
    public async Task Someone_who_maintains_neither_profile_is_forbidden()
    {
        var (owner, ownerToken) = await SignInAsync("maya@example.org");
        var parent = await CreatePersonAsync(owner, ownerToken, "Hari");
        var child = await CreatePersonAsync(owner, ownerToken, "Maya");
        var (other, token) = await SignInAsync("gita@example.org");

        using var response = await CreateLinkAsync(other, token, parent, child, "Biological");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("link.not_endpoint_maintainer", await CodeAsync(response));
    }

    [Fact]
    public async Task Second_link_for_the_same_pair_is_a_conflict()
    {
        var (browser, token) = await SignInAsync("maya@example.org");
        var parent = await CreatePersonAsync(browser, token, "Hari");
        var child = await CreatePersonAsync(browser, token, "Maya");
        (await CreateLinkAsync(browser, token, parent, child, "Biological")).Dispose();

        using var response = await CreateLinkAsync(browser, token, parent, child, "Adoptive");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("link.duplicate", await CodeAsync(response));
    }

    [Fact]
    [Trait("AC", "AC-02.6")]
    public async Task Link_that_would_close_a_cycle_is_rejected()
    {
        var (browser, token) = await SignInAsync("maya@example.org");
        var grandparent = await CreatePersonAsync(browser, token, "Ram");
        var parent = await CreatePersonAsync(browser, token, "Hari");
        var child = await CreatePersonAsync(browser, token, "Maya");
        (await CreateLinkAsync(browser, token, grandparent, parent, "Biological")).Dispose();
        (await CreateLinkAsync(browser, token, parent, child, "Biological")).Dispose();

        using var response = await CreateLinkAsync(browser, token, child, grandparent, "Biological");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("link.cycle_detected", await CodeAsync(response));
    }

    [Fact]
    public async Task Self_link_is_rejected()
    {
        var (browser, token) = await SignInAsync("maya@example.org");
        var person = await CreatePersonAsync(browser, token, "Maya");

        using var response = await CreateLinkAsync(browser, token, person, person, "Biological");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("link.self_link", await CodeAsync(response));
    }

    [Theory]
    [InlineData("Step")]
    [InlineData(null)]
    public async Task Unknown_type_is_rejected(string? type)
    {
        var (browser, token) = await SignInAsync("maya@example.org");
        var parent = await CreatePersonAsync(browser, token, "Hari");
        var child = await CreatePersonAsync(browser, token, "Maya");

        using var response = await CreateLinkAsync(browser, token, parent, child, type);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Link_to_an_unknown_profile_is_not_found()
    {
        var (browser, token) = await SignInAsync("maya@example.org");
        var child = await CreatePersonAsync(browser, token, "Maya");

        using var response = await CreateLinkAsync(browser, token, Guid.CreateVersion7(), child, "Biological");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("person.not_found", await CodeAsync(response));
    }

    [Fact]
    public async Task Relatives_of_an_unknown_profile_are_not_found()
    {
        var (browser, _) = await SignInAsync("maya@example.org");

        using var response = await browser.GetAsync($"/api/people/{Guid.CreateVersion7()}/relatives", Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("person.not_found", await CodeAsync(response));
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

    private static Task<HttpResponseMessage> CreateLinkAsync(
        HttpClient browser, string token, Guid parentId, Guid childId, string? type) =>
        SendAsync(browser, token, HttpMethod.Post, "/api/relationships", new { parentId, childId, type });

    private static async Task<HttpResponseMessage> SendAsync(HttpClient browser, string token, HttpMethod method, string url, object body)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-XSRF-TOKEN", token);
        return await browser.SendAsync(request, Cancellation);
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(Cancellation))!["code"].ToString();
}
