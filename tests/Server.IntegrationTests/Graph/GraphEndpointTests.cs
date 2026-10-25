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
    [InlineData("/api/graph/overview")]
    [InlineData("/api/people/0199f6a2-0000-7000-8000-000000000001/bansawali")]
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
    [Trait("AC", "AC-02.17")]
    public async Task Local_view_carries_birth_order_and_only_birth_dates_shown_to_others()
    {
        var (browser, token) = await SignInAsync(UniqueEmail());
        var mother = await CreatePersonAsync(browser, token, "Sita");
        var shown = await CreatePersonAsync(browser, token, new
        {
            firstName = "Maya",
            birthDate = new { calendar = "Ad", year = 1990, month = 4 },
            birthDateShown = true,
        });
        var hidden = await CreatePersonAsync(browser, token, new
        {
            firstName = "Rina",
            birthDate = new { calendar = "Ad", year = 1980 },
        });
        using var shownLink = await CreateLinkAsync(browser, token, mother, shown, "Biological");
        (await CreateLinkAsync(browser, token, mother, hidden, "Biological")).Dispose();
        var shownLinkId = (await shownLink.Content.ReadFromJsonAsync<Dictionary<string, object>>(Cancellation))!["id"].ToString();
        using var order = new HttpRequestMessage(HttpMethod.Put, $"/api/people/{mother}/children/order")
        {
            Content = JsonContent.Create(new { linkIds = new[] { shownLinkId } }),
        };
        order.Headers.Add("X-XSRF-TOKEN", token);
        using var ordered = await browser.SendAsync(order, Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, ordered.StatusCode);

        var view = await browser.GetFromJsonAsync<LocalViewResponse>($"/api/people/{mother}/graph", Cancellation);

        Assert.Equal(1, view!.Links.Single(link => link.ChildId == shown).BirthOrder);
        Assert.Null(view.Links.Single(link => link.ChildId == hidden).BirthOrder);
        // The maintainer sees the hidden date on the person page, but it never orders children (D-039).
        Assert.Equal(new DateOnly(1990, 4, 1), view.People.Single(row => row.Person.Id == shown).Person.BirthSortDate);
        Assert.Null(view.People.Single(row => row.Person.Id == hidden).Person.BirthSortDate);
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
    [Trait("AC", "AC-08.4")]
    public async Task Overview_returns_everyone_connected_to_your_profiles_in_one_response()
    {
        var (other, otherToken) = await SignInAsync(UniqueEmail());
        var stranger = await CreatePersonAsync(other, otherToken, "Hari");
        var (browser, token) = await SignInAsync(UniqueEmail());
        var mother = await CreatePersonAsync(browser, token, "Sita");
        var you = await CreatePersonAsync(browser, token, "Maya");
        var daughter = await CreatePersonAsync(browser, token, "Rina");
        (await CreateLinkAsync(browser, token, mother, you, "Biological")).Dispose();
        (await CreateLinkAsync(browser, token, you, daughter, "Adoptive")).Dispose();
        (await CreateLinkAsync(browser, token, stranger, daughter, "Biological")).Dispose();

        var overview = await browser.GetFromJsonAsync<GraphOverviewResponse>("/api/graph/overview", Cancellation);

        // The link from Hari waits for the other account's approval, so Hari is not reached.
        Assert.Equal(["Maya", "Rina", "Sita"], overview!.People.Select(person => person.FirstName).Order());
        Assert.All(overview.People, person => Assert.True(person.MaintainedByYou));
        Assert.Equal(
            [(mother, you, "Biological"), (you, daughter, "Adoptive")],
            overview.Links.Select(link => (link.ParentId, link.ChildId, link.Type)).OrderByDescending(link => link.Item3));
        Assert.False(overview.IsTruncated);
    }

    [Fact]
    public async Task Overview_without_profiles_is_empty()
    {
        var (browser, _) = await SignInAsync(UniqueEmail());

        var overview = await browser.GetFromJsonAsync<GraphOverviewResponse>("/api/graph/overview", Cancellation);

        Assert.Empty(overview!.People);
        Assert.Empty(overview.Links);
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

    [Fact]
    [Trait("AC", "AC-08.21")]
    [Trait("AC", "AC-08.22")]
    [Trait("AC", "AC-08.23")]
    public async Task Bansawali_follows_sons_by_default_and_every_child_on_request()
    {
        var (browser, token) = await SignInAsync(UniqueEmail());
        var hari = await CreatePersonAsync(browser, token, new { firstName = "Hari", lastName = "Neupane", gender = "Male" });
        var gauri = await CreatePersonAsync(browser, token, new { firstName = "Gauri", gender = "Female" });
        var ram = await CreatePersonAsync(browser, token, new { firstName = "Ram", gender = "Male" });
        var sita = await CreatePersonAsync(browser, token, new { firstName = "Sita", gender = "Female" });
        var gita = await CreatePersonAsync(browser, token, "Gita");
        (await CreateLinkAsync(browser, token, hari, ram, "Biological")).Dispose();
        (await CreateLinkAsync(browser, token, hari, sita, "Biological")).Dispose();
        (await CreateLinkAsync(browser, token, sita, gita, "Biological")).Dispose();
        (await SendAsync(browser, token, "/api/marriages", new { spouseId = hari, otherSpouseId = gauri })).Dispose();

        var sons = await browser.GetFromJsonAsync<BansawaliResponse>($"/api/people/{hari}/bansawali", Cancellation);
        var all = await browser.GetFromJsonAsync<BansawaliResponse>(
            $"/api/people/{hari}/bansawali?line=AllChildren", Cancellation);

        Assert.Equal((hari, "Sons"), (sons!.RootId, sons.Line));
        Assert.Equal(
            [("Hari", 0, true), ("Ram", 1, true), ("Sita", 1, false)],
            sons.People.Select(row => (row.Person.FirstName, row.Generation, row.Continues)).OrderBy(row => row.FirstName));
        Assert.Equal("Neupane", sons.People[0].Person.LastName);
        Assert.Equal(2, sons.Links.Count);
        Assert.Equal(gauri, Assert.Single(sons.Spouses).Id);
        Assert.False(Assert.Single(sons.Marriages).IsFormer);
        Assert.Equal("AllChildren", all!.Line);
        Assert.Contains(all.People, row => row.Person.Id == gita && row.Generation == 2);
    }

    [Fact]
    public async Task Bansawali_rejects_an_unknown_line()
    {
        var (browser, token) = await SignInAsync(UniqueEmail());
        var hari = await CreatePersonAsync(browser, token, "Hari");

        using var response = await browser.GetAsync($"/api/people/{hari}/bansawali?line=Daughters", Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private async Task<(HttpClient Browser, string Token)> SignInAsync(string email)
    {
        var browser = factory.CreateBrowser();
        return (browser, await AccountTestFactory.SignInAsync(browser, email, Cancellation));
    }

    private static Task<Guid> CreatePersonAsync(HttpClient browser, string token, string firstName) =>
        CreatePersonAsync(browser, token, new { firstName });

    private static async Task<Guid> CreatePersonAsync(HttpClient browser, string token, object person)
    {
        using var response = await SendAsync(browser, token, "/api/people", person);
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
