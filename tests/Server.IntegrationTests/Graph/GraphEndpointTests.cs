using System.Net;
using System.Net.Http.Json;
using Server.Graph;
using Server.IntegrationTests.Accounts;
using Server.People;

namespace Server.IntegrationTests.Graph;

public class GraphEndpointTests(AccountTestFactory factory) : IClassFixture<AccountTestFactory>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [Trait("AC", "AC-05.6")]
    [InlineData("/api/people/0199f6a2-0000-7000-8000-000000000001/graph")]
    [InlineData("/api/people/0199f6a2-0000-7000-8000-000000000001/connections/0199f6a2-0000-7000-8000-000000000002")]
    public async Task Graph_reads_require_a_session(string url)
    {
        using var browser = factory.CreateBrowser();

        using var response = await browser.GetAsync(url, Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("AC", "AC-08.4")]
    [Trait("AC", "AC-08.3")]
    public async Task Local_view_returns_the_centre_its_nearest_people_and_the_active_links_between_them()
    {
        var (browser, token) = await SignInAsync(UniqueEmail());
        var grandmother = await CreatePersonAsync(browser, token, "Gita");
        var mother = await CreatePersonAsync(browser, token, "Sita");
        var you = await CreatePersonAsync(browser, token, "Maya");
        (await CreateLinkAsync(browser, token, grandmother, mother, "Biological")).Dispose();
        (await CreateLinkAsync(browser, token, mother, you, "Adoptive")).Dispose();

        var view = await browser.GetFromJsonAsync<LocalViewResponse>($"/api/people/{you}/graph", Cancellation);

        Assert.Equal(you, view!.CentreId);
        Assert.Equal(
            [("Maya", 0, false), ("Sita", 1, false), ("Gita", 2, false)],
            view.People.Select(row => (row.Person.FirstName, row.Generation, row.HasMore)));
        Assert.All(view.People, row => Assert.True(row.Person.MaintainedByYou));
        Assert.Equal(
            [(mother, you, "Adoptive"), (grandmother, mother, "Biological")],
            view.Links.Select(link => (link.ParentId, link.ChildId, link.Type)).OrderBy(link => link.Item3));
        Assert.False(view.IsTruncated);
    }

    [Fact]
    [Trait("AC", "AC-05.5")]
    public async Task Local_view_leaves_out_people_reached_only_through_an_inactive_link()
    {
        var (other, otherToken) = await SignInAsync(UniqueEmail());
        var mother = await CreatePersonAsync(other, otherToken, "Sita");
        var (browser, token) = await SignInAsync(UniqueEmail());
        var you = await CreatePersonAsync(browser, token, "Maya");
        (await CreateLinkAsync(browser, token, mother, you, "Biological")).Dispose();

        var view = await browser.GetFromJsonAsync<LocalViewResponse>($"/api/people/{you}/graph", Cancellation);

        Assert.Equal(["Maya"], view!.People.Select(row => row.Person.FirstName));
        Assert.Empty(view.Links);
    }

    [Fact]
    [Trait("AC", "AC-08.7")]
    public async Task Connections_show_each_step_with_its_relation_and_type()
    {
        var (browser, token) = await SignInAsync(UniqueEmail());
        var mother = await CreatePersonAsync(browser, token, "Sita");
        var you = await CreatePersonAsync(browser, token, "Maya");
        var sister = await CreatePersonAsync(browser, token, "Rina");
        (await CreateLinkAsync(browser, token, mother, you, "Biological")).Dispose();
        (await CreateLinkAsync(browser, token, mother, sister, "Adoptive")).Dispose();

        var connections = await browser.GetFromJsonAsync<ConnectionsResponse>(
            $"/api/people/{you}/connections/{sister}", Cancellation);

        Assert.True(connections!.SearchComplete);
        Assert.False(connections.MorePathsExist);
        var path = Assert.Single(connections.Paths);
        Assert.Equal("Maya", path.Start.FirstName);
        Assert.Equal(
            [("Parent", "Biological", "Sita"), ("Child", "Adoptive", "Rina")],
            path.Steps.Select(step => (step.Relation, step.Type, step.Person.FirstName)));
    }

    [Theory]
    [InlineData("graph")]
    [InlineData("connections/0199f6a2-0000-7000-8000-000000000002")]
    public async Task Unknown_person_is_not_found(string path)
    {
        var (browser, _) = await SignInAsync(UniqueEmail());

        using var response = await browser.GetAsync($"/api/people/{Guid.NewGuid()}/{path}", Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("person.not_found", await CodeAsync(response));
    }

    [Fact]
    public async Task Graph_query_that_times_out_is_service_unavailable()
    {
        var (browser, token) = await SignInAsync(UniqueEmail());
        var you = await CreatePersonAsync(browser, token, "Maya");
        factory.Graph.TimesOut = true;
        try
        {
            using var response = await browser.GetAsync($"/api/people/{you}/graph", Cancellation);

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal("graph.timeout", await CodeAsync(response));
        }
        finally
        {
            factory.Graph.TimesOut = false;
        }
    }

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private async Task<(HttpClient Browser, string Token)> SignInAsync(string email)
    {
        var browser = factory.CreateBrowser();
        return (browser, await AccountTestFactory.SignInAsync(browser, email, Cancellation));
    }

    private static async Task<Guid> CreatePersonAsync(HttpClient browser, string token, string firstName)
    {
        using var response = await SendAsync(browser, token, "/api/people", new { firstName });
        return (await response.Content.ReadFromJsonAsync<PersonResponse>(Cancellation))!.Id;
    }

    private static async Task<HttpResponseMessage> CreateLinkAsync(
        HttpClient browser, string token, Guid parentId, Guid childId, string type)
    {
        var response = await SendAsync(browser, token, "/api/relationships", new { parentId, childId, type });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return response;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient browser, string token, string url, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-XSRF-TOKEN", token);
        return await browser.SendAsync(request, Cancellation);
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(Cancellation))!["code"].ToString();
}
