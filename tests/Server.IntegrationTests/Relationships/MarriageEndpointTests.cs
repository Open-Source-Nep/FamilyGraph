using System.Net;
using System.Net.Http.Json;
using FamilyGraph.Business.Relationships;
using Server.Graph;
using Server.IntegrationTests.Accounts;
using Server.People;
using Server.Relationships;

namespace Server.IntegrationTests.Relationships;

public class MarriageEndpointTests(AccountTestFactory factory) : IClassFixture<AccountTestFactory>
{
    private const string AnyId = "0199f6a2-0000-7000-8000-000000000001";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("POST", "/api/marriages")]
    [InlineData("GET", $"/api/people/{AnyId}/spouses")]
    [InlineData("GET", "/api/marriages/inactive")]
    [InlineData("POST", $"/api/marriages/{AnyId}/approve")]
    [InlineData("POST", $"/api/marriages/{AnyId}/disagree")]
    [InlineData("PUT", $"/api/marriages/{AnyId}/former")]
    [InlineData("DELETE", $"/api/marriages/{AnyId}")]
    public async Task Marriages_require_a_session(string method, string url)
    {
        using var browser = factory.CreateBrowser();

        using var response = await browser.SendAsync(new HttpRequestMessage(new HttpMethod(method), url), Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("AC", "AC-02.11")]
    public async Task Maintainer_of_both_creates_an_active_marriage_listed_on_both_sides()
    {
        var (browser, token) = await SignInAsync(UniqueEmail());
        var ram = await CreatePersonAsync(browser, token, "Ram", "Male");
        var sita = await CreatePersonAsync(browser, token, "Sita", "Female");

        using var response = await CreateMarriageAsync(browser, token, ram, sita);
        var created = await response.Content.ReadFromJsonAsync<CreatedMarriageResponse>(Cancellation);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(created!.IsActive);
        Assert.Equal([ram, sita], new[] { created.FirstSpouseId, created.SecondSpouseId }.Order());
        var ofRam = Assert.Single((await SpousesAsync(browser, ram)).Spouses);
        Assert.Equal(("Sita", "Female", false), (ofRam.FirstName, ofRam.Gender, ofRam.IsFormer));
        Assert.Equal("Ram", Assert.Single((await SpousesAsync(browser, sita)).Spouses).FirstName);
        Assert.IsType<MarriageCreated>(factory.People.AuditEvents[^1].Event);
    }

    [Fact]
    [Trait("AC", "AC-02.12")]
    public async Task A_marriage_with_someone_elses_profile_waits_for_their_approval()
    {
        var (other, otherToken) = await SignInAsync(UniqueEmail());
        var sita = await CreatePersonAsync(other, otherToken, "Sita");
        var (browser, token) = await SignInAsync(UniqueEmail());
        var ram = await CreatePersonAsync(browser, token, "Ram");

        var id = await CreatedMarriageIdAsync(browser, token, ram, sita);

        Assert.Empty((await SpousesAsync(browser, ram)).Spouses);
        var waiting = await other.GetFromJsonAsync<MarriageConsentListResponse>("/api/marriages/inactive", Cancellation);
        Assert.Equal(id, Assert.Single(waiting!.Marriages).Id);

        using var approve = await SendAsync(other, otherToken, HttpMethod.Post, $"/api/marriages/{id}/approve", new { spouseId = sita });
        var approved = await approve.Content.ReadFromJsonAsync<MarriageConsentResponse>(Cancellation);

        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        Assert.True(approved!.IsActive);
        Assert.Single((await SpousesAsync(browser, ram)).Spouses);
    }

    [Fact]
    [Trait("AC", "AC-02.12")]
    public async Task Disagreeing_for_someone_elses_spouse_is_forbidden_and_a_sole_maintainer_must_delete()
    {
        var (other, otherToken) = await SignInAsync(UniqueEmail());
        var sita = await CreatePersonAsync(other, otherToken, "Sita");
        var (browser, token) = await SignInAsync(UniqueEmail());
        var ram = await CreatePersonAsync(browser, token, "Ram");
        var gita = await CreatePersonAsync(browser, token, "Gita");
        var withSita = await CreatedMarriageIdAsync(browser, token, ram, sita);
        var withGita = await CreatedMarriageIdAsync(browser, token, ram, gita);

        using var forbidden = await SendAsync(browser, token, HttpMethod.Post, $"/api/marriages/{withSita}/disagree", new { spouseId = sita });
        using var mustDelete = await SendAsync(browser, token, HttpMethod.Post, $"/api/marriages/{withGita}/disagree", new { spouseId = gita });
        using var notASpouse = await SendAsync(browser, token, HttpMethod.Post, $"/api/marriages/{withGita}/approve", new { spouseId = sita });

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("marriage.consent_not_maintainer", await CodeAsync(forbidden));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, mustDelete.StatusCode);
        Assert.Equal("marriage.sole_maintainer_must_delete", await CodeAsync(mustDelete));
        Assert.Equal("marriage.not_a_spouse", await CodeAsync(notASpouse));
    }

    [Fact]
    [Trait("AC", "AC-02.13")]
    public async Task Self_and_duplicate_marriages_are_rejected()
    {
        var (browser, token) = await SignInAsync(UniqueEmail());
        var ram = await CreatePersonAsync(browser, token, "Ram");
        var sita = await CreatePersonAsync(browser, token, "Sita");
        (await CreateMarriageAsync(browser, token, ram, sita)).Dispose();

        using var self = await CreateMarriageAsync(browser, token, ram, ram);
        using var duplicate = await CreateMarriageAsync(browser, token, sita, ram);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, self.StatusCode);
        Assert.Equal("marriage.self", await CodeAsync(self));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("marriage.duplicate", await CodeAsync(duplicate));
    }

    [Fact]
    [Trait("AC", "AC-02.16")]
    public async Task A_parent_and_child_cannot_be_married_nor_spouses_linked_as_parent_and_child()
    {
        var (browser, token) = await SignInAsync(UniqueEmail());
        var hari = await CreatePersonAsync(browser, token, "Hari");
        var maya = await CreatePersonAsync(browser, token, "Maya");
        var ram = await CreatePersonAsync(browser, token, "Ram");
        (await SendAsync(browser, token, HttpMethod.Post, "/api/relationships", new { parentId = hari, childId = maya, type = "Biological" })).Dispose();
        (await CreateMarriageAsync(browser, token, maya, ram)).Dispose();

        using var marriage = await CreateMarriageAsync(browser, token, maya, hari);
        using var link = await SendAsync(browser, token, HttpMethod.Post, "/api/relationships", new { parentId = ram, childId = maya, type = "Adoptive" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, marriage.StatusCode);
        Assert.Equal("marriage.parent_child", await CodeAsync(marriage));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, link.StatusCode);
        Assert.Equal("link.spouses", await CodeAsync(link));
    }

    [Fact]
    [Trait("AC", "AC-02.14")]
    [Trait("AC", "AC-02.15")]
    public async Task Either_maintainer_marks_it_former_and_back_and_it_stays_in_the_graph()
    {
        var (other, otherToken) = await SignInAsync(UniqueEmail());
        var sita = await CreatePersonAsync(other, otherToken, "Sita");
        var (browser, token) = await SignInAsync(UniqueEmail());
        var ram = await CreatePersonAsync(browser, token, "Ram");
        var id = await CreatedMarriageIdAsync(browser, token, ram, sita);
        (await SendAsync(other, otherToken, HttpMethod.Post, $"/api/marriages/{id}/approve", new { spouseId = sita })).Dispose();

        using var former = await SendAsync(other, otherToken, HttpMethod.Put, $"/api/marriages/{id}/former", new { isFormer = true });
        using var again = await SendAsync(browser, token, HttpMethod.Put, $"/api/marriages/{id}/former", new { isFormer = true });

        Assert.Equal(HttpStatusCode.NoContent, former.StatusCode);
        Assert.Equal("marriage.former_unchanged", await CodeAsync(again));
        Assert.True(Assert.Single((await SpousesAsync(browser, ram)).Spouses).IsFormer);
        var view = await browser.GetFromJsonAsync<LocalViewResponse>($"/api/people/{ram}/graph", Cancellation);
        Assert.True(Assert.Single(view!.Marriages).IsFormer);
        Assert.Contains(view.People, row => row.Person.Id == sita && row.Generation == 0);

        using var current = await SendAsync(browser, token, HttpMethod.Put, $"/api/marriages/{id}/former", new { isFormer = false });

        Assert.Equal(HttpStatusCode.NoContent, current.StatusCode);
        Assert.False(Assert.Single((await SpousesAsync(browser, ram)).Spouses).IsFormer);
    }

    [Fact]
    [Trait("AC", "AC-08.9")]
    public async Task A_path_goes_through_a_marriage_as_a_spouse_step()
    {
        var (browser, token) = await SignInAsync(UniqueEmail());
        var maya = await CreatePersonAsync(browser, token, "Maya");
        var ram = await CreatePersonAsync(browser, token, "Ram");
        var father = await CreatePersonAsync(browser, token, "Dasharath");
        (await CreateMarriageAsync(browser, token, maya, ram)).Dispose();
        (await SendAsync(browser, token, HttpMethod.Post, "/api/relationships", new { parentId = father, childId = ram, type = "Biological" })).Dispose();

        var connections = await browser.GetFromJsonAsync<ConnectionsResponse>($"/api/people/{maya}/connections/{father}", Cancellation);

        var path = Assert.Single(connections!.Paths);
        Assert.Equal([("Spouse", "Marriage"), ("Parent", "Biological")], path.Steps.Select(step => (step.Relation, step.Type)));
    }

    [Fact]
    [Trait("AC", "AC-02.12")]
    public async Task A_sole_maintainer_deletes_the_marriage()
    {
        var (browser, token) = await SignInAsync(UniqueEmail());
        var ram = await CreatePersonAsync(browser, token, "Ram");
        var sita = await CreatePersonAsync(browser, token, "Sita");
        var id = await CreatedMarriageIdAsync(browser, token, ram, sita);

        using var deleted = await SendAsync(browser, token, HttpMethod.Delete, $"/api/marriages/{id}", new { });
        using var again = await SendAsync(browser, token, HttpMethod.Delete, $"/api/marriages/{id}", new { });
        using var unknown = await SendAsync(browser, token, HttpMethod.Delete, $"/api/marriages/{Guid.NewGuid()}", new { });

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal("marriage.deleted", await CodeAsync(again));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Empty((await SpousesAsync(browser, ram)).Spouses);
    }

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private static async Task<SpouseListResponse> SpousesAsync(HttpClient browser, Guid personId) =>
        (await browser.GetFromJsonAsync<SpouseListResponse>($"/api/people/{personId}/spouses", Cancellation))!;

    private static async Task<Guid> CreatedMarriageIdAsync(HttpClient browser, string token, Guid spouseId, Guid otherSpouseId)
    {
        using var response = await CreateMarriageAsync(browser, token, spouseId, otherSpouseId);
        return (await response.Content.ReadFromJsonAsync<CreatedMarriageResponse>(Cancellation))!.Id;
    }

    private static Task<HttpResponseMessage> CreateMarriageAsync(HttpClient browser, string token, Guid spouseId, Guid otherSpouseId) =>
        SendAsync(browser, token, HttpMethod.Post, "/api/marriages", new { spouseId, otherSpouseId });

    private async Task<(HttpClient Browser, string Token)> SignInAsync(string email)
    {
        var browser = factory.CreateBrowser();
        return (browser, await AccountTestFactory.SignInAsync(browser, email, Cancellation));
    }

    private static async Task<Guid> CreatePersonAsync(HttpClient browser, string token, string firstName, string? gender = null)
    {
        using var response = await SendAsync(browser, token, HttpMethod.Post, "/api/people", new { firstName, gender });
        return (await response.Content.ReadFromJsonAsync<PersonResponse>(Cancellation))!.Id;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient browser, string token, HttpMethod method, string url, object body)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-XSRF-TOKEN", token);
        return await browser.SendAsync(request, Cancellation);
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(Cancellation))!["code"].ToString();
}
