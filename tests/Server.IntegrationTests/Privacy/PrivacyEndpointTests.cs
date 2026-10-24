using System.Net;
using System.Net.Http.Json;
using Server.Graph;
using Server.IntegrationTests.Accounts;
using Server.People;
using Server.Relationships;

namespace Server.IntegrationTests.Privacy;

/// <summary>
/// A Private profile's name must be absent from every response to anyone but its maintainer and
/// administrators, while its active links stay in the graph (PRD 05, #18). Each read endpoint is
/// checked on the raw response body, so no field can carry the name.
/// </summary>
public class PrivacyEndpointTests(AccountTestFactory factory) : IClassFixture<AccountTestFactory>
{
    private const string HiddenFirstName = "Zebediah";
    private const string HiddenLastName = "Quillfeather";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Changing_visibility_requires_a_session()
    {
        using var browser = factory.CreateBrowser();

        using var response = await browser.PutAsJsonAsync(
            "/api/people/0199f6a2-0000-7000-8000-000000000001/visibility", new { visibility = "Private" }, Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("AC", "AC-05.7")]
    public async Task New_profile_is_public_and_its_maintainer_can_make_it_private_and_public_again()
    {
        var (browser, token) = await SignInAsync(UniqueEmail());
        var id = await CreatePersonAsync(browser, token, "Maya");
        Assert.Equal("Public", (await GetPersonAsync(browser, id)).Visibility);

        using var toPrivate = await SetVisibilityAsync(browser, token, id, "Private");
        Assert.Equal(HttpStatusCode.NoContent, toPrivate.StatusCode);
        var asMaintainer = await GetPersonAsync(browser, id);
        Assert.Equal(("Private", "Maya", null), (asMaintainer.Visibility, asMaintainer.FirstName, asMaintainer.Placeholder));

        using var toPublic = await SetVisibilityAsync(browser, token, id, "Public");
        Assert.Equal(HttpStatusCode.NoContent, toPublic.StatusCode);
        Assert.Equal("Public", (await GetPersonAsync(browser, id)).Visibility);
    }

    [Fact]
    [Trait("AC", "AC-05.7")]
    public async Task Only_the_maintainer_changes_visibility()
    {
        var (owner, ownerToken) = await SignInAsync(UniqueEmail());
        var id = await CreatePersonAsync(owner, ownerToken, "Maya");
        var (other, otherToken) = await SignInAsync(UniqueEmail());

        using var response = await SetVisibilityAsync(other, otherToken, id, "Private");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("person.not_maintainer", await CodeAsync(response));
        Assert.Equal("Public", (await GetPersonAsync(owner, id)).Visibility);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Hidden")]
    public async Task Unknown_visibility_is_a_bad_request(string? visibility)
    {
        var (browser, token) = await SignInAsync(UniqueEmail());
        var id = await CreatePersonAsync(browser, token, "Maya");

        using var response = await SetVisibilityAsync(browser, token, id, visibility);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Setting_the_current_visibility_again_is_a_conflict()
    {
        var (browser, token) = await SignInAsync(UniqueEmail());
        var id = await CreatePersonAsync(browser, token, "Maya");

        using var response = await SetVisibilityAsync(browser, token, id, "Public");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("person.visibility_unchanged", await CodeAsync(response));
    }

    [Fact]
    [Trait("AC", "AC-05.1")]
    [Trait("AC", "AC-05.8")]
    public async Task Profile_page_shows_a_private_placeholder_to_others_and_the_name_to_maintainer_and_administrators()
    {
        var family = await PrivateParentAsync(approved: false);
        var (admin, _) = await SignInAsync(AccountTestFactory.AdministratorEmail);

        var (body, asOther) = await ReadAsync<PersonResponse>(family.Viewer, $"/api/people/{family.Hidden}");
        var asMaintainer = await GetPersonAsync(family.Owner, family.Hidden);
        var asAdministrator = await GetPersonAsync(admin, family.Hidden);

        AssertHidden(body);
        Assert.Equal(
            (family.Hidden, null, null, null, "Private", "Private", false),
            (asOther.Id, asOther.FirstName, asOther.MiddleName, asOther.LastName, asOther.Placeholder, asOther.Visibility,
                asOther.MaintainedByYou));
        Assert.Equal((HiddenFirstName, null, true), (asMaintainer.FirstName, asMaintainer.Placeholder, asMaintainer.MaintainedByYou));
        Assert.Equal((HiddenFirstName, null, false), (asAdministrator.FirstName, asAdministrator.Placeholder, asAdministrator.MaintainedByYou));
    }

    [Fact]
    [Trait("AC", "AC-05.1")]
    [Trait("AC", "AC-05.9")]
    public async Task Relatives_show_a_private_parent_as_a_placeholder_with_the_link_type()
    {
        var family = await PrivateParentAsync(approved: true);

        var (body, relatives) = await ReadAsync<DirectRelativesResponse>(family.Viewer, $"/api/people/{family.Child}/relatives");

        AssertHidden(body);
        var parent = Assert.Single(relatives.Parents);
        Assert.Equal((family.Hidden, null, "Private", "Adoptive"), (parent.Id, parent.FirstName, parent.Placeholder, parent.Type));
    }

    [Fact]
    [Trait("AC", "AC-05.5")]
    public async Task Making_a_profile_private_does_not_make_its_pending_link_active()
    {
        var family = await PrivateParentAsync(approved: false);

        var relatives = await family.Viewer.GetFromJsonAsync<DirectRelativesResponse>(
            $"/api/people/{family.Child}/relatives", Cancellation);

        Assert.Empty(relatives!.Parents);
    }

    [Fact]
    [Trait("AC", "AC-05.1")]
    public async Task Inactive_links_and_consent_responses_show_a_private_endpoint_as_a_placeholder()
    {
        var family = await PrivateParentAsync(approved: true);
        using var withdrawn = await SendAsync(
            family.Viewer, family.ViewerToken, HttpMethod.Post, $"/api/relationships/{family.Link}/disagree", new { endpoint = "Child" });
        var withdrawnBody = await withdrawn.Content.ReadAsStringAsync(Cancellation);

        var (listBody, list) = await ReadAsync<LinkConsentListResponse>(family.Viewer, "/api/relationships/inactive");

        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);
        AssertHidden(withdrawnBody);
        AssertHidden(listBody);
        var link = Assert.Single(list.Links);
        Assert.Equal((family.Hidden, null, "Private", "Approved"), (link.Parent.Id, link.Parent.FirstName, link.Parent.Placeholder, link.Parent.Approval));
        Assert.Equal("Maya", link.Child.FirstName);
    }

    [Fact]
    [Trait("AC", "AC-05.2")]
    [Trait("AC", "AC-05.4")]
    public async Task Local_view_passes_through_a_private_placeholder()
    {
        var family = await PrivateParentAsync(approved: true);

        var (body, view) = await ReadAsync<LocalViewResponse>(family.Viewer, $"/api/people/{family.Child}/graph");

        AssertHidden(body);
        Assert.Equal(
            [(family.Child, "Maya", null), (family.Hidden, null, "Private"), (family.Grandparent, "Gita", null)],
            view.People.Select(row => (row.Person.Id, row.Person.FirstName, row.Person.Placeholder)));
        Assert.Equal(2, view.Links.Count);
    }

    [Fact]
    [Trait("AC", "AC-05.2")]
    public async Task Local_view_centred_on_a_private_person_shows_the_placeholder()
    {
        var family = await PrivateParentAsync(approved: true);

        var (body, view) = await ReadAsync<LocalViewResponse>(family.Viewer, $"/api/people/{family.Hidden}/graph");

        AssertHidden(body);
        Assert.Equal("Private", view.People[0].Person.Placeholder);
        Assert.Equal(3, view.People.Count);
    }

    [Fact]
    [Trait("AC", "AC-05.4")]
    public async Task Connections_pass_through_a_private_placeholder()
    {
        var family = await PrivateParentAsync(approved: true);

        var (body, connections) = await ReadAsync<ConnectionsResponse>(
            family.Viewer, $"/api/people/{family.Child}/connections/{family.Grandparent}");

        AssertHidden(body);
        var path = Assert.Single(connections.Paths);
        Assert.Equal(
            [("Parent", "Adoptive", null, "Private"), ("Parent", "Biological", "Gita", null)],
            path.Steps.Select(step => (step.Relation, step.Type, step.Person.FirstName, step.Person.Placeholder)));
    }

    [Fact]
    [Trait("AC", "AC-05.2")]
    public async Task Overview_shows_a_private_placeholder_and_the_people_beyond_it()
    {
        var family = await PrivateParentAsync(approved: true);

        var (body, overview) = await ReadAsync<GraphOverviewResponse>(family.Viewer, "/api/graph/overview");

        AssertHidden(body);
        Assert.Equal(
            [(family.Child, "Maya", null), (family.Hidden, null, "Private"), (family.Grandparent, "Gita", null)],
            overview.People.Select(person => (person.Id, person.FirstName, person.Placeholder)));
    }

    [Fact]
    [Trait("AC", "AC-05.14")]
    public async Task A_private_profile_shows_its_gender_only_to_its_maintainer_and_administrators()
    {
        var family = await PrivateParentAsync(approved: false);
        var (admin, _) = await SignInAsync(AccountTestFactory.AdministratorEmail);

        var (asOther, asMaintainer, asAdministrator) = (
            await GetPersonAsync(family.Viewer, family.Hidden),
            await GetPersonAsync(family.Owner, family.Hidden),
            await GetPersonAsync(admin, family.Hidden));

        Assert.Equal(((string?)null, "Male", "Male"), (asOther.Gender, asMaintainer.Gender, asAdministrator.Gender));
    }

    /// <summary>The hidden person is the only one with a gender, so no "Male" may appear either (D-030).</summary>
    private static void AssertHidden(string body)
    {
        Assert.DoesNotContain("\"Male\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain(HiddenFirstName, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(HiddenLastName, body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Another account's Private profile "Zebediah Quillfeather" (Male), with a Public parent Gita, is the
    /// adoptive parent of the viewer's Maya. The link waits for the owner's approval unless
    /// <paramref name="approved"/>.
    /// </summary>
    private async Task<Family> PrivateParentAsync(bool approved)
    {
        var (owner, ownerToken) = await SignInAsync(UniqueEmail());
        var hidden = await CreatePersonAsync(owner, ownerToken, HiddenFirstName, HiddenLastName, "Male");
        var grandparent = await CreatePersonAsync(owner, ownerToken, "Gita");
        (await CreateLinkAsync(owner, ownerToken, grandparent, hidden, "Biological")).Dispose();
        (await SetVisibilityAsync(owner, ownerToken, hidden, "Private")).Dispose();

        var (viewer, viewerToken) = await SignInAsync(UniqueEmail());
        var child = await CreatePersonAsync(viewer, viewerToken, "Maya");
        using var created = await CreateLinkAsync(viewer, viewerToken, hidden, child, "Adoptive");
        var link = (await created.Content.ReadFromJsonAsync<CreatedLinkResponse>(Cancellation))!.Id;
        if (approved)
        {
            using var approval = await SendAsync(
                owner, ownerToken, HttpMethod.Post, $"/api/relationships/{link}/approve", new { endpoint = "Parent" });
            Assert.Equal(HttpStatusCode.OK, approval.StatusCode);
        }

        return new Family(owner, viewer, viewerToken, hidden, grandparent, child, link);
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

    private static async Task<PersonResponse> GetPersonAsync(HttpClient browser, Guid id) =>
        (await browser.GetFromJsonAsync<PersonResponse>($"/api/people/{id}", Cancellation))!;

    private static async Task<Guid> CreatePersonAsync(
        HttpClient browser, string token, string firstName, string? lastName = null, string? gender = null)
    {
        using var response = await SendAsync(browser, token, HttpMethod.Post, "/api/people", new { firstName, lastName, gender });
        return (await response.Content.ReadFromJsonAsync<PersonResponse>(Cancellation))!.Id;
    }

    private static async Task<HttpResponseMessage> CreateLinkAsync(
        HttpClient browser, string token, Guid parentId, Guid childId, string type)
    {
        var response = await SendAsync(browser, token, HttpMethod.Post, "/api/relationships", new { parentId, childId, type });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return response;
    }

    private static Task<HttpResponseMessage> SetVisibilityAsync(HttpClient browser, string token, Guid id, string? visibility) =>
        SendAsync(browser, token, HttpMethod.Put, $"/api/people/{id}/visibility", new { visibility });

    private static async Task<HttpResponseMessage> SendAsync(HttpClient browser, string token, HttpMethod method, string url, object body)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-XSRF-TOKEN", token);
        return await browser.SendAsync(request, Cancellation);
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(Cancellation))!["code"].ToString();

    private sealed record Family(
        HttpClient Owner, HttpClient Viewer, string ViewerToken, Guid Hidden, Guid Grandparent, Guid Child, Guid Link);
}
