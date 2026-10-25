using System.Net;
using System.Net.Http.Json;
using FamilyGraph.Business.Relationships;
using Server.IntegrationTests.Accounts;
using Server.People;
using Server.Relationships;

namespace Server.IntegrationTests.Relationships;

/// <summary>Administrators approving pending sides on behalf of maintainers (D-040, #105).</summary>
public class AdministratorApprovalEndpointTests(AccountTestFactory factory) : IClassFixture<AccountTestFactory>
{
    private const string AnyId = "0199f6a2-0000-7000-8000-000000000001";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("GET", "/api/relationships/pending")]
    [InlineData("POST", $"/api/relationships/{AnyId}/approve-as-administrator")]
    [InlineData("GET", "/api/marriages/pending")]
    [InlineData("POST", $"/api/marriages/{AnyId}/approve-as-administrator")]
    public async Task Administrator_approval_requires_a_session(string method, string url)
    {
        using var browser = factory.CreateBrowser();

        using var response = await browser.SendAsync(new HttpRequestMessage(new HttpMethod(method), url), Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("AC", "AC-03.6")]
    [Trait("AC", "AC-03.9")]
    public async Task A_user_who_is_not_an_administrator_cannot_list_or_approve_on_behalf()
    {
        var (bob, bobToken) = await SignInAsync(UniqueEmail());
        var maya = await CreatePersonAsync(bob, bobToken, "Maya");
        var (alice, token) = await SignInAsync(UniqueEmail());
        var hari = await CreatePersonAsync(alice, token, "Hari");
        var linkId = await CreateLinkAsync(alice, token, hari, maya);

        using var links = await alice.GetAsync("/api/relationships/pending", Cancellation);
        using var marriages = await alice.GetAsync("/api/marriages/pending", Cancellation);
        using var approve = await SendAsync(
            alice, token, $"/api/relationships/{linkId}/approve-as-administrator", new { endpoint = "Child" });

        Assert.Equal(
            (HttpStatusCode.Forbidden, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden),
            (links.StatusCode, marriages.StatusCode, approve.StatusCode));
        Assert.Equal("consent.not_administrator", await CodeAsync(approve));
    }

    [Fact]
    [Trait("AC", "AC-03.6")]
    [Trait("AC", "AC-03.8")]
    [Trait("AC", "AC-03.9")]
    public async Task Administrator_lists_a_pending_link_and_approves_it_on_behalf_of_its_maintainer()
    {
        var (bob, bobToken) = await SignInAsync(UniqueEmail());
        var maya = await CreatePersonAsync(bob, bobToken, "Maya");
        var (alice, token) = await SignInAsync(UniqueEmail());
        var hari = await CreatePersonAsync(alice, token, "Hari");
        var linkId = await CreateLinkAsync(alice, token, hari, maya);
        var (admin, adminToken) = await SignInAsync(AccountTestFactory.AdministratorEmail);

        var pending = await admin.GetFromJsonAsync<LinkConsentListResponse>("/api/relationships/pending", Cancellation);
        var listed = Assert.Single(pending!.Links, link => link.Id == linkId);
        Assert.Equal(("Pending", false), (listed.Child.Approval, listed.Child.MaintainedByYou));

        using var response = await SendAsync(
            admin, adminToken, $"/api/relationships/{linkId}/approve-as-administrator", new { endpoint = "Child" });
        var approved = await response.Content.ReadFromJsonAsync<LinkConsentResponse>(Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(approved!.IsActive);
        var recorded = Assert.IsType<LinkEndpointApprovedByAdministrator>(factory.People.AuditEvents[^1].Event);
        Assert.Equal((linkId, maya), (recorded.LinkId.Value, recorded.Endpoint.Value));
        Assert.NotEqual(recorded.ActorId, recorded.MaintainerId);
        var after = await admin.GetFromJsonAsync<LinkConsentListResponse>("/api/relationships/pending", Cancellation);
        Assert.DoesNotContain(after!.Links, link => link.Id == linkId);
    }

    [Fact]
    [Trait("AC", "AC-03.7")]
    public async Task Administrator_cannot_restore_a_declined_side()
    {
        var (bob, bobToken) = await SignInAsync(UniqueEmail());
        var maya = await CreatePersonAsync(bob, bobToken, "Maya");
        var (alice, token) = await SignInAsync(UniqueEmail());
        var hari = await CreatePersonAsync(alice, token, "Hari");
        var linkId = await CreateLinkAsync(alice, token, hari, maya);
        using var declined = await SendAsync(bob, bobToken, $"/api/relationships/{linkId}/disagree", new { endpoint = "Child" });
        var (admin, adminToken) = await SignInAsync(AccountTestFactory.AdministratorEmail);

        using var response = await SendAsync(
            admin, adminToken, $"/api/relationships/{linkId}/approve-as-administrator", new { endpoint = "Child" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("consent.not_pending", await CodeAsync(response));
    }

    [Fact]
    [Trait("AC", "AC-03.6")]
    [Trait("AC", "AC-03.9")]
    public async Task Administrator_lists_and_approves_a_pending_marriage()
    {
        var (bob, bobToken) = await SignInAsync(UniqueEmail());
        var sita = await CreatePersonAsync(bob, bobToken, "Sita");
        var (alice, token) = await SignInAsync(UniqueEmail());
        var ram = await CreatePersonAsync(alice, token, "Ram");
        using var created = await SendAsync(alice, token, "/api/marriages", new { spouseId = ram, otherSpouseId = sita });
        var marriageId = (await created.Content.ReadFromJsonAsync<CreatedMarriageResponse>(Cancellation))!.Id;
        var (admin, adminToken) = await SignInAsync(AccountTestFactory.AdministratorEmail);

        var pending = await admin.GetFromJsonAsync<MarriageConsentListResponse>("/api/marriages/pending", Cancellation);
        Assert.Contains(pending!.Marriages, marriage => marriage.Id == marriageId);

        using var response = await SendAsync(
            admin, adminToken, $"/api/marriages/{marriageId}/approve-as-administrator", new { spouseId = sita });
        var approved = await response.Content.ReadFromJsonAsync<MarriageConsentResponse>(Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(approved!.IsActive);
        Assert.IsType<MarriageSpouseApprovedByAdministrator>(factory.People.AuditEvents[^1].Event);
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

    private static async Task<Guid> CreateLinkAsync(HttpClient browser, string token, Guid parentId, Guid childId)
    {
        using var response = await SendAsync(browser, token, "/api/relationships", new { parentId, childId, type = "Biological" });
        return (await response.Content.ReadFromJsonAsync<CreatedLinkResponse>(Cancellation))!.Id;
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
