using System.Net;
using System.Net.Http.Json;
using FamilyGraph.Business.People;
using Server.IntegrationTests.Accounts;
using Server.People;

namespace Server.IntegrationTests.People;

public class PeopleEndpointTests(AccountTestFactory factory) : IClassFixture<AccountTestFactory>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("GET", "/api/people/maintained")]
    [InlineData("GET", "/api/people/search?q=maya")]
    [InlineData("GET", "/api/people/0199f6a2-0000-7000-8000-000000000001")]
    public async Task People_require_a_session(string method, string url)
    {
        using var browser = factory.CreateBrowser();

        using var response = await browser.SendAsync(new HttpRequestMessage(new HttpMethod(method), url), Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("AC", "AC-01.1")]
    [Trait("AC", "AC-01.7")]
    public async Task Create_with_only_a_first_name_returns_the_profile_maintained_by_the_creator()
    {
        var (browser, token) = await SignInAsync("maya@example.org");

        using var response = await SendAsync(browser, token, HttpMethod.Post, "/api/people", new { firstName = "Maya" });
        var person = await response.Content.ReadFromJsonAsync<PersonResponse>(Cancellation);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/api/people/{person!.Id}", response.Headers.Location!.AbsolutePath);
        Assert.Equal("Maya", person.FirstName);
        Assert.Null(person.MiddleName);
        Assert.Null(person.LastName);
        Assert.True(person.MaintainedByYou);
    }

    [Fact]
    public async Task Create_requires_the_antiforgery_token()
    {
        var (browser, _) = await SignInAsync("maya@example.org");

        using var response = await browser.PostAsJsonAsync("/api/people", new { firstName = "Maya" }, Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public async Task Create_without_a_first_name_is_a_domain_rule_violation(string? firstName)
    {
        var (browser, token) = await SignInAsync("maya@example.org");

        using var response = await SendAsync(browser, token, HttpMethod.Post, "/api/people", new { firstName, lastName = "Rai" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("person.first_name_required", await CodeAsync(response));
    }

    [Fact]
    public async Task Name_parts_longer_than_the_limit_are_rejected()
    {
        var (browser, token) = await SignInAsync("maya@example.org");

        using var response = await SendAsync(
            browser, token, HttpMethod.Post, "/api/people", new { firstName = new string('a', PersonNameRequest.MaxPartLength + 1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Another_signed_in_user_can_view_the_profile_but_does_not_maintain_it()
    {
        var id = await CreateAsync("maya@example.org", "Maya");
        var (other, _) = await SignInAsync("gita@example.org");

        var person = await other.GetFromJsonAsync<PersonResponse>($"/api/people/{id}", Cancellation);

        Assert.Equal("Maya", person!.FirstName);
        Assert.False(person.MaintainedByYou);
    }

    [Theory]
    [InlineData("0199f6a2-0000-7000-8000-000000000001")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Unknown_profile_is_not_found(string id)
    {
        var (browser, _) = await SignInAsync("maya@example.org");

        using var response = await browser.GetAsync($"/api/people/{id}", Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("person.not_found", await CodeAsync(response));
    }

    [Fact]
    [Trait("AC", "AC-01.8")]
    public async Task Maintainer_renames_the_profile()
    {
        var (browser, token) = await SignInAsync("maya@example.org");
        using var created = await SendAsync(browser, token, HttpMethod.Post, "/api/people", new { firstName = "Maya" });
        var id = (await created.Content.ReadFromJsonAsync<PersonResponse>(Cancellation))!.Id;

        using var response = await SendAsync(
            browser, token, HttpMethod.Put, $"/api/people/{id}/name", new { firstName = "Maya", middleName = "Devi", lastName = "Rai" });
        var person = await browser.GetFromJsonAsync<PersonResponse>($"/api/people/{id}", Cancellation);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(("Maya", "Devi", "Rai"), (person!.FirstName, person.MiddleName, person.LastName));
    }

    [Fact]
    [Trait("AC", "AC-01.8")]
    public async Task Rename_by_someone_other_than_the_maintainer_is_forbidden()
    {
        var id = await CreateAsync("maya@example.org", "Maya");
        var (other, token) = await SignInAsync("gita@example.org");

        using var response = await SendAsync(other, token, HttpMethod.Put, $"/api/people/{id}/name", new { firstName = "Gita" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("person.not_maintainer", await CodeAsync(response));
        Assert.Equal("Maya", factory.People.People[new PersonId(id)].Name.FirstName);
    }

    [Fact]
    [Trait("AC", "AC-01.21")]
    [Trait("AC", "AC-01.22")]
    public async Task Gender_is_optional_at_creation_and_the_maintainer_sets_and_clears_it()
    {
        var (browser, token) = await SignInAsync("maya@example.org");
        using var created = await SendAsync(browser, token, HttpMethod.Post, "/api/people", new { firstName = "Maya", gender = "Female" });
        var person = (await created.Content.ReadFromJsonAsync<PersonResponse>(Cancellation))!;
        Assert.Equal("Female", person.Gender);

        using var changed = await SendAsync(browser, token, HttpMethod.Put, $"/api/people/{person.Id}/gender", new { gender = "Other" });
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        Assert.Equal("Other", (await browser.GetFromJsonAsync<PersonResponse>($"/api/people/{person.Id}", Cancellation))!.Gender);

        using var cleared = await SendAsync(browser, token, HttpMethod.Put, $"/api/people/{person.Id}/gender", new { gender = (string?)null });
        Assert.Equal(HttpStatusCode.NoContent, cleared.StatusCode);
        Assert.Null((await browser.GetFromJsonAsync<PersonResponse>($"/api/people/{person.Id}", Cancellation))!.Gender);
    }

    [Fact]
    [Trait("AC", "AC-01.22")]
    public async Task Setting_the_gender_by_someone_other_than_the_maintainer_is_forbidden()
    {
        var id = await CreateAsync("maya@example.org", "Maya");
        var (other, token) = await SignInAsync("gita@example.org");

        using var response = await SendAsync(other, token, HttpMethod.Put, $"/api/people/{id}/gender", new { gender = "Female" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("person.not_maintainer", await CodeAsync(response));
        Assert.Null(factory.People.People[new PersonId(id)].Gender);
    }

    [Theory]
    [Trait("AC", "AC-01.21")]
    [InlineData("female")]
    [InlineData("Unknown")]
    public async Task An_unknown_gender_is_a_bad_request(string gender)
    {
        var (browser, token) = await SignInAsync("maya@example.org");
        var id = await CreateAsync("maya@example.org", "Maya");

        using var created = await SendAsync(browser, token, HttpMethod.Post, "/api/people", new { firstName = "Maya", gender });
        using var changed = await SendAsync(browser, token, HttpMethod.Put, $"/api/people/{id}/gender", new { gender });

        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.BadRequest), (created.StatusCode, changed.StatusCode));
    }

    [Fact]
    [Trait("AC", "AC-01.2")]
    public async Task Maintained_list_shows_every_profile_the_user_maintains_and_no_others()
    {
        var (browser, token) = await SignInAsync("maya@example.org");
        foreach (var name in new[] { "Maya", "Asha" })
        {
            using var _ = await SendAsync(browser, token, HttpMethod.Post, "/api/people", new { firstName = name });
        }

        await CreateAsync("gita@example.org", "Gita");

        var list = await browser.GetFromJsonAsync<ProfileListResponse>("/api/people/maintained", Cancellation);

        Assert.Equal(["Asha", "Maya"], list!.People.Select(person => person.FirstName));
        Assert.False(list.IsTruncated);
    }

    [Fact]
    [Trait("AC", "AC-08.11")]
    public async Task Search_finds_public_profiles_maintained_by_someone_else()
    {
        var id = await CreateAsync("gita@example.org", "Zarina");
        var (browser, _) = await SignInAsync("maya@example.org");

        var result = await browser.GetFromJsonAsync<PersonSearchResponse>("/api/people/search?q=zari", Cancellation);

        var person = Assert.Single(result!.People);
        Assert.Equal((id, "Zarina", false), (person.Id, person.FirstName, person.MaintainedByYou));
        Assert.False(result.IsTruncated);
    }

    [Fact]
    public async Task Search_longer_than_a_full_name_is_a_bad_request()
    {
        var (browser, _) = await SignInAsync("maya@example.org");

        using var response = await browser.GetAsync($"/api/people/search?q={new string('a', 303)}", Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<(HttpClient Browser, string Token)> SignInAsync(string email)
    {
        var browser = factory.CreateBrowser();
        return (browser, await AccountTestFactory.SignInAsync(browser, email, Cancellation));
    }

    private async Task<Guid> CreateAsync(string email, string firstName)
    {
        var (browser, token) = await SignInAsync(email);
        using var response = await SendAsync(browser, token, HttpMethod.Post, "/api/people", new { firstName });
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
