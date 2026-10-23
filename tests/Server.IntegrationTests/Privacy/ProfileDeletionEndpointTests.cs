using System.Net;
using System.Net.Http.Json;
using FamilyGraph.Business.People;
using Server.Graph;
using Server.IntegrationTests.Accounts;
using Server.People;
using Server.Relationships;

namespace Server.IntegrationTests.Privacy;

/// <summary>
/// Deleting a profile erases its name for good and leaves a <c>Deleted</c> placeholder that keeps
/// its active links (PRD 05, #18). Responses are checked on the raw body for the erased name.
/// </summary>
public class ProfileDeletionEndpointTests(AccountTestFactory factory) : IClassFixture<AccountTestFactory>
{
    private const string ErasedFirstName = "Zebediah";
    private const string ErasedLastName = "Quillfeather";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Deleting_a_profile_requires_a_session()
    {
        using var browser = factory.CreateBrowser();

        using var response = await browser.DeleteAsync("/api/people/0199f6a2-0000-7000-8000-000000000001", Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("AC", "AC-05.3")]
    [Trait("AC", "AC-05.10")]
    public async Task Maintainer_deletes_a_profile_which_everyone_then_sees_as_deleted()
    {
        var (browser, token) = await SignInAsync(UniqueEmail());
        var id = await CreatePersonAsync(browser, token, ErasedFirstName, ErasedLastName);
        var (admin, _) = await SignInAsync(AccountTestFactory.AdministratorEmail);

        using var response = await DeleteAsync(browser, token, id);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        foreach (var viewer in new[] { browser, admin })
        {
            using var read = await viewer.GetAsync($"/api/people/{id}", Cancellation);
            var body = await read.Content.ReadAsStringAsync(Cancellation);
            AssertErased(body);
            var person = (await read.Content.ReadFromJsonAsync<PersonResponse>(Cancellation))!;
            Assert.Equal((id, null, "Deleted", false), (person.Id, person.FirstName, person.Placeholder, person.MaintainedByYou));
        }

        var maintained = await browser.GetFromJsonAsync<ProfileListResponse>("/api/people/maintained", Cancellation);
        Assert.DoesNotContain(maintained!.People, person => person.Id == id);
    }

    [Fact]
    [Trait("AC", "AC-05.11")]
    public async Task Administrator_may_delete_another_accounts_profile_and_the_audit_says_so()
    {
        var (owner, ownerToken) = await SignInAsync(UniqueEmail());
        var id = await CreatePersonAsync(owner, ownerToken, "Maya");
        var (other, otherToken) = await SignInAsync(UniqueEmail());
        var (admin, adminToken) = await SignInAsync(AccountTestFactory.AdministratorEmail);

        using var refused = await DeleteAsync(other, otherToken, id);
        using var deleted = await DeleteAsync(admin, adminToken, id);

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal("person.not_maintainer", await CodeAsync(refused));
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var auditEvent = factory.People.AuditEvents
            .Select(entry => entry.Event).OfType<PersonDeleted>().Single(e => e.PersonId.Value == id);
        Assert.Equal(ProfileDeleter.Administrator, auditEvent.DeletedBy);
    }

    [Fact]
    public async Task Deleted_profile_cannot_be_deleted_renamed_or_made_private()
    {
        var (browser, token) = await SignInAsync(UniqueEmail());
        var id = await CreatePersonAsync(browser, token, "Maya");
        (await DeleteAsync(browser, token, id)).Dispose();

        using var again = await DeleteAsync(browser, token, id);
        using var renamed = await SendAsync(browser, token, HttpMethod.Put, $"/api/people/{id}/name", new { firstName = "Maya" });
        using var hidden = await SendAsync(browser, token, HttpMethod.Put, $"/api/people/{id}/visibility", new { visibility = "Private" });

        Assert.All([again, renamed, hidden], response =>
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode));
        Assert.Equal("person.deleted", await CodeAsync(again));
    }

    [Fact]
    public async Task Deleting_an_unknown_profile_is_not_found()
    {
        var (browser, token) = await SignInAsync(UniqueEmail());

        using var response = await DeleteAsync(browser, token, Guid.CreateVersion7());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("AC", "AC-05.12")]
    public async Task No_new_link_can_be_created_with_a_deleted_profile()
    {
        var (browser, token) = await SignInAsync(UniqueEmail());
        var deleted = await CreatePersonAsync(browser, token, "Hari");
        var child = await CreatePersonAsync(browser, token, "Maya");
        (await DeleteAsync(browser, token, deleted)).Dispose();

        using var response = await SendAsync(
            browser, token, HttpMethod.Post, "/api/relationships", new { parentId = deleted, childId = child, type = "Biological" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("link.person_deleted", await CodeAsync(response));
    }

    [Fact]
    [Trait("AC", "AC-05.3")]
    [Trait("AC", "AC-05.4")]
    [Trait("AC", "AC-05.9")]
    public async Task Deleted_profile_stays_in_relatives_and_the_graph_with_its_active_links()
    {
        var (browser, token) = await SignInAsync(UniqueEmail());
        var grandparent = await CreatePersonAsync(browser, token, "Gita");
        var deleted = await CreatePersonAsync(browser, token, ErasedFirstName, ErasedLastName);
        var child = await CreatePersonAsync(browser, token, "Maya");
        (await CreateLinkAsync(browser, token, grandparent, deleted, "Biological")).Dispose();
        (await CreateLinkAsync(browser, token, deleted, child, "Adoptive")).Dispose();
        (await DeleteAsync(browser, token, deleted)).Dispose();

        var (relativesBody, relatives) = await ReadAsync<DirectRelativesResponse>(browser, $"/api/people/{child}/relatives");
        var (viewBody, view) = await ReadAsync<LocalViewResponse>(browser, $"/api/people/{child}/graph");
        var (pathBody, connections) = await ReadAsync<ConnectionsResponse>(browser, $"/api/people/{child}/connections/{grandparent}");
        var (overviewBody, overview) = await ReadAsync<GraphOverviewResponse>(browser, "/api/graph/overview");

        Assert.All([relativesBody, viewBody, pathBody, overviewBody], AssertErased);
        var parent = Assert.Single(relatives.Parents);
        Assert.Equal((deleted, "Deleted", "Adoptive"), (parent.Id, parent.Placeholder, parent.Type));
        Assert.Equal(
            [(child, null), (deleted, "Deleted"), (grandparent, null)],
            view.People.Select(row => (row.Person.Id, row.Person.Placeholder)));
        Assert.Equal(2, view.Links.Count);
        Assert.Equal(
            ["Deleted", null],
            Assert.Single(connections.Paths).Steps.Select(step => step.Person.Placeholder));
        Assert.Contains(overview.People, person => person.Id == deleted && person.Placeholder == "Deleted");
    }

    private static void AssertErased(string body)
    {
        Assert.DoesNotContain(ErasedFirstName, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ErasedLastName, body, StringComparison.OrdinalIgnoreCase);
    }

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private async Task<(HttpClient Browser, string Token)> SignInAsync(string email)
    {
        var browser = factory.CreateBrowser();
        return (browser, await AccountTestFactory.SignInAsync(browser, email, Cancellation));
    }

    private static async Task<(string Body, T Value)> ReadAsync<T>(HttpClient browser, string url)
    {
        using var response = await browser.GetAsync(url, Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(Cancellation);
        return (body, (await response.Content.ReadFromJsonAsync<T>(Cancellation))!);
    }

    private static async Task<Guid> CreatePersonAsync(HttpClient browser, string token, string firstName, string? lastName = null)
    {
        using var response = await SendAsync(browser, token, HttpMethod.Post, "/api/people", new { firstName, lastName });
        return (await response.Content.ReadFromJsonAsync<PersonResponse>(Cancellation))!.Id;
    }

    private static async Task<HttpResponseMessage> CreateLinkAsync(
        HttpClient browser, string token, Guid parentId, Guid childId, string type)
    {
        var response = await SendAsync(browser, token, HttpMethod.Post, "/api/relationships", new { parentId, childId, type });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return response;
    }

    private static async Task<HttpResponseMessage> DeleteAsync(HttpClient browser, string token, Guid id)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/people/{id}");
        request.Headers.Add("X-XSRF-TOKEN", token);
        return await browser.SendAsync(request, Cancellation);
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
