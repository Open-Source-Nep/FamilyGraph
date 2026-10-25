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
    [InlineData("GET", "/api/relationships/inactive")]
    [InlineData("POST", "/api/relationships/0199f6a2-0000-7000-8000-000000000001/approve")]
    [InlineData("POST", "/api/relationships/0199f6a2-0000-7000-8000-000000000001/disagree")]
    [InlineData("DELETE", "/api/relationships/0199f6a2-0000-7000-8000-000000000001")]
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

    [Fact]
    [Trait("AC", "AC-03.1")]
    [Trait("AC", "AC-03.3")]
    public async Task Approving_activates_withdrawing_deactivates_and_restoring_reactivates()
    {
        var (other, otherToken) = await SignInAsync(UniqueEmail());
        var parent = await CreatePersonAsync(other, otherToken, "Gita");
        var (browser, token) = await SignInAsync(UniqueEmail());
        var child = await CreatePersonAsync(browser, token, "Maya");
        var linkId = await CreatedLinkIdAsync(browser, token, parent, child);

        using var approved = await ConsentAsync(other, otherToken, linkId, "approve", "Parent");
        var approvedLink = await approved.Content.ReadFromJsonAsync<LinkConsentResponse>(Cancellation);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.True(approvedLink!.IsActive);
        Assert.Equal(("Approved", true), (approvedLink.Parent.Approval, approvedLink.Parent.MaintainedByYou));
        Assert.Equal(("Approved", false), (approvedLink.Child.Approval, approvedLink.Child.MaintainedByYou));
        var listed = Assert.Single((await RelativesAsync(browser, child)).Parents);
        Assert.Equal((linkId, false), (listed.LinkId, listed.MaintainedByYou));

        using var withdrawn = await ConsentAsync(browser, token, linkId, "disagree", "Child");
        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);
        Assert.False((await withdrawn.Content.ReadFromJsonAsync<LinkConsentResponse>(Cancellation))!.IsActive);
        Assert.IsType<LinkEndpointDisagreed>(factory.People.AuditEvents[^1].Event);
        Assert.Empty((await RelativesAsync(browser, child)).Parents);

        using var restored = await ConsentAsync(browser, token, linkId, "approve", "Child");
        Assert.True((await restored.Content.ReadFromJsonAsync<LinkConsentResponse>(Cancellation))!.IsActive);
        Assert.IsType<LinkApprovalRestored>(factory.People.AuditEvents[^1].Event);
        Assert.Single((await RelativesAsync(browser, child)).Parents);
    }

    [Fact]
    [Trait("AC", "AC-03.5")]
    public async Task Only_the_endpoint_maintainer_can_change_its_approval()
    {
        var (other, otherToken) = await SignInAsync(UniqueEmail());
        var parent = await CreatePersonAsync(other, otherToken, "Gita");
        var (browser, token) = await SignInAsync(UniqueEmail());
        var child = await CreatePersonAsync(browser, token, "Maya");
        var linkId = await CreatedLinkIdAsync(browser, token, parent, child);

        using var response = await ConsentAsync(browser, token, linkId, "approve", "Parent");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("link.consent_not_maintainer", await CodeAsync(response));
    }

    [Fact]
    public async Task Repeating_the_current_approval_is_a_conflict()
    {
        var (other, otherToken) = await SignInAsync(UniqueEmail());
        var parent = await CreatePersonAsync(other, otherToken, "Gita");
        var (browser, token) = await SignInAsync(UniqueEmail());
        var child = await CreatePersonAsync(browser, token, "Maya");
        var linkId = await CreatedLinkIdAsync(browser, token, parent, child);

        using var response = await ConsentAsync(browser, token, linkId, "approve", "Child");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("link.consent_unchanged", await CodeAsync(response));
    }

    [Fact]
    public async Task Sole_maintainer_cannot_disagree()
    {
        var (browser, token) = await SignInAsync(UniqueEmail());
        var parent = await CreatePersonAsync(browser, token, "Hari");
        var child = await CreatePersonAsync(browser, token, "Maya");
        var linkId = await CreatedLinkIdAsync(browser, token, parent, child);

        using var response = await ConsentAsync(browser, token, linkId, "disagree", "Parent");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("link.sole_maintainer_must_delete", await CodeAsync(response));
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("disagree")]
    public async Task Consent_on_an_unknown_link_is_not_found(string action)
    {
        var (browser, token) = await SignInAsync(UniqueEmail());

        using var response = await ConsentAsync(browser, token, Guid.CreateVersion7(), action, "Parent");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("link.not_found", await CodeAsync(response));
    }

    [Theory]
    [InlineData("Sibling")]
    [InlineData(null)]
    public async Task Unknown_endpoint_is_rejected(string? endpoint)
    {
        var (browser, token) = await SignInAsync(UniqueEmail());

        using var response = await ConsentAsync(browser, token, Guid.CreateVersion7(), "approve", endpoint);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    [Trait("AC", "AC-03.4")]
    public async Task Sole_maintainer_deletes_a_link_and_it_cannot_be_deleted_again()
    {
        var (browser, token) = await SignInAsync(UniqueEmail());
        var parent = await CreatePersonAsync(browser, token, "Hari");
        var child = await CreatePersonAsync(browser, token, "Maya");
        var linkId = await CreatedLinkIdAsync(browser, token, parent, child);
        Assert.True(Assert.Single((await RelativesAsync(browser, child)).Parents).MaintainedByYou);

        using var deleted = await DeleteAsync(browser, token, linkId);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.IsType<LinkDeleted>(factory.People.AuditEvents[^1].Event);
        Assert.Empty((await RelativesAsync(browser, child)).Parents);

        using var again = await DeleteAsync(browser, token, linkId);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("link.deleted", await CodeAsync(again));
    }

    [Fact]
    [Trait("AC", "AC-03.4")]
    public async Task Different_maintainer_cannot_delete_and_withdraws_instead()
    {
        var (other, otherToken) = await SignInAsync(UniqueEmail());
        var parent = await CreatePersonAsync(other, otherToken, "Gita");
        var (browser, token) = await SignInAsync(UniqueEmail());
        var child = await CreatePersonAsync(browser, token, "Maya");
        var linkId = await CreatedLinkIdAsync(browser, token, parent, child);
        using var _ = await ConsentAsync(other, otherToken, linkId, "approve", "Parent");

        using var refused = await DeleteAsync(browser, token, linkId);

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal("link.not_sole_maintainer", await CodeAsync(refused));
        Assert.Single((await RelativesAsync(browser, child)).Parents);

        using var withdrawn = await ConsentAsync(browser, token, linkId, "disagree", "Child");
        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);
        Assert.IsType<LinkEndpointDisagreed>(factory.People.AuditEvents[^1].Event);
    }

    [Fact]
    public async Task Deleting_an_unknown_link_is_not_found()
    {
        var (browser, token) = await SignInAsync(UniqueEmail());

        using var response = await DeleteAsync(browser, token, Guid.CreateVersion7());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("link.not_found", await CodeAsync(response));
    }

    [Fact]
    public async Task Inactive_links_list_those_waiting_for_you_first()
    {
        var (other, otherToken) = await SignInAsync(UniqueEmail());
        var gita = await CreatePersonAsync(other, otherToken, "Gita");
        var ram = await CreatePersonAsync(other, otherToken, "Ram");
        var (browser, token) = await SignInAsync(UniqueEmail());
        var maya = await CreatePersonAsync(browser, token, "Maya");
        var waitingForOther = await CreatedLinkIdAsync(browser, token, gita, maya);
        var waitingForYou = await CreatedLinkIdAsync(other, otherToken, ram, maya);

        var list = await browser.GetFromJsonAsync<LinkConsentListResponse>("/api/relationships/inactive", Cancellation);

        Assert.Equal([waitingForYou, waitingForOther], list!.Links.Select(link => link.Id));
        Assert.False(list.IsTruncated);
        Assert.Equal(("Ram", "Approved", "Pending"), (list.Links[0].Parent.FirstName, list.Links[0].Parent.Approval, list.Links[0].Child.Approval));
        Assert.True(list.Links[0].Child.MaintainedByYou);
    }

    [Fact]
    [Trait("AC", "AC-02.8")]
    [Trait("AC", "AC-02.9")]
    public async Task Parent_maintainer_puts_the_children_in_birth_order()
    {
        var (browser, token) = await SignInAsync(UniqueEmail());
        var parent = await CreatePersonAsync(browser, token, "Hari");
        var elder = await CreatedLinkIdAsync(browser, token, parent, await CreatePersonAsync(browser, token, "Zarina"));
        var younger = await CreatedLinkIdAsync(browser, token, parent, await CreatePersonAsync(browser, token, "Asha"));

        using var response = await SendAsync(
            browser, token, HttpMethod.Put, $"/api/people/{parent}/children/order", new { linkIds = new[] { elder, younger } });
        var children = (await RelativesAsync(browser, parent)).Children;

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal([(elder, (int?)1), (younger, 2)], children.Select(child => (child.LinkId, child.BirthOrder)));
    }

    [Fact]
    [Trait("AC", "AC-02.9")]
    public async Task Only_the_parents_maintainer_orders_the_children()
    {
        var (browser, token) = await SignInAsync(UniqueEmail());
        var parent = await CreatePersonAsync(browser, token, "Hari");
        var link = await CreatedLinkIdAsync(browser, token, parent, await CreatePersonAsync(browser, token, "Maya"));
        var (other, otherToken) = await SignInAsync(UniqueEmail());

        using var response = await SendAsync(
            other, otherToken, HttpMethod.Put, $"/api/people/{parent}/children/order", new { linkIds = new[] { link } });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("link.birth_order_not_parent_maintainer", await CodeAsync(response));
    }

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private async Task<DirectRelativesResponse> RelativesAsync(HttpClient browser, Guid personId) =>
        (await browser.GetFromJsonAsync<DirectRelativesResponse>($"/api/people/{personId}/relatives", Cancellation))!;

    private static async Task<Guid> CreatedLinkIdAsync(HttpClient browser, string token, Guid parentId, Guid childId)
    {
        using var response = await CreateLinkAsync(browser, token, parentId, childId, "Biological");
        return (await response.Content.ReadFromJsonAsync<CreatedLinkResponse>(Cancellation))!.Id;
    }

    private static Task<HttpResponseMessage> ConsentAsync(
        HttpClient browser, string token, Guid linkId, string action, string? endpoint) =>
        SendAsync(browser, token, HttpMethod.Post, $"/api/relationships/{linkId}/{action}", new { endpoint });

    private static async Task<HttpResponseMessage> DeleteAsync(HttpClient browser, string token, Guid linkId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/relationships/{linkId}");
        request.Headers.Add("X-XSRF-TOKEN", token);
        return await browser.SendAsync(request, Cancellation);
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
