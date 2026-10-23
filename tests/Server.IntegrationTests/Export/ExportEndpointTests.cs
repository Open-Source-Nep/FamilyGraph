using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FamilyGraph.Business.Export;
using Server.Accounts;
using Server.IntegrationTests.Accounts;
using Server.People;

namespace Server.IntegrationTests.Export;

/// <summary>An administrator's download of the whole instance (D-029, #75).</summary>
public class ExportEndpointTests(AccountTestFactory factory) : IClassFixture<AccountTestFactory>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Export_requires_a_session()
    {
        using var browser = factory.CreateBrowser();

        using var response = await browser.GetAsync("/api/export", Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("AC", "AC-09.5")]
    public async Task A_user_who_is_not_an_administrator_cannot_export_and_nothing_is_recorded()
    {
        var (browser, _) = await SignInAsync(UniqueEmail());
        var exportsBefore = Exports();

        using var response = await browser.GetAsync("/api/export", Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("export.not_administrator", problem.GetProperty("code").GetString());
        Assert.Equal(exportsBefore, Exports());
    }

    [Fact]
    [Trait("AC", "AC-09.4")]
    [Trait("AC", "AC-09.6")]
    [Trait("AC", "AC-09.7")]
    public async Task Administrator_downloads_profiles_links_and_maintainer_emails_and_the_export_is_recorded()
    {
        var email = UniqueEmail();
        var (owner, token) = await SignInAsync(email);
        var parent = await CreatePersonAsync(owner, token, "Hari", "Sharma");
        var child = await CreatePersonAsync(owner, token, "Maya");
        using var hidden = await SendAsync(owner, token, HttpMethod.Put, $"/api/people/{child}/visibility", new { visibility = "Private" });
        using var link = await SendAsync(owner, token, HttpMethod.Post, "/api/relationships", new { parentId = parent, childId = child, type = "Adoptive" });
        var (admin, _) = await SignInAsync(AccountTestFactory.AdministratorEmail);

        using var response = await admin.GetAsync("/api/export", Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("familygraph-export-", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var file = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal("familygraph-export", file.GetProperty("format").GetString());
        Assert.Equal(1, file.GetProperty("version").GetInt32());

        var account = Single(file, "accounts", "email", email);
        var person = Single(file, "people", "id", child.ToString());
        Assert.Equal(("Maya", "Private", account.GetProperty("id").GetGuid()), (
            person.GetProperty("firstName").GetString(),
            person.GetProperty("visibility").GetString(),
            person.GetProperty("maintainerId").GetGuid()));
        var exported = Single(file, "links", "childId", child.ToString());
        Assert.Equal((parent, "Adoptive", "Approved", "Approved"), (
            exported.GetProperty("parentId").GetGuid(),
            exported.GetProperty("type").GetString(),
            exported.GetProperty("parentApproval").GetString(),
            exported.GetProperty("childApproval").GetString()));
        var adminAccount = await admin.GetFromJsonAsync<AccountResponse>("/api/account", Cancellation);
        Assert.Contains(factory.People.AuditEvents, entry => entry.Event is DataExported e && e.ActorId.Value == adminAccount!.Id);
    }

    [Fact]
    [Trait("AC", "AC-09.6")]
    public async Task A_deleted_profile_is_exported_without_its_name()
    {
        var (owner, token) = await SignInAsync(UniqueEmail());
        var deleted = await CreatePersonAsync(owner, token, "Zebediah", "Quillfeather");
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/people/{deleted}");
        request.Headers.Add("X-XSRF-TOKEN", token);
        (await owner.SendAsync(request, Cancellation)).Dispose();
        var (admin, _) = await SignInAsync(AccountTestFactory.AdministratorEmail);

        var body = await admin.GetStringAsync("/api/export", Cancellation);

        Assert.DoesNotContain("Zebediah", body, StringComparison.Ordinal);
        Assert.DoesNotContain("[erased]", body, StringComparison.Ordinal);
        var person = Single(JsonDocument.Parse(body).RootElement, "people", "id", deleted.ToString());
        Assert.Equal(JsonValueKind.Null, person.GetProperty("firstName").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, person.GetProperty("deletedAt").ValueKind);
    }

    private int Exports() => factory.People.AuditEvents.Count(entry => entry.Event is DataExported);

    private static JsonElement Single(JsonElement file, string section, string property, string value) =>
        Assert.Single(file.GetProperty(section).EnumerateArray(), item => item.GetProperty(property).GetString() == value);

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    private async Task<(HttpClient Browser, string Token)> SignInAsync(string email)
    {
        var browser = factory.CreateBrowser();
        return (browser, await AccountTestFactory.SignInAsync(browser, email, Cancellation));
    }

    private static async Task<Guid> CreatePersonAsync(HttpClient browser, string token, string firstName, string? lastName = null)
    {
        using var response = await SendAsync(browser, token, HttpMethod.Post, "/api/people", new { firstName, lastName });
        return (await response.Content.ReadFromJsonAsync<PersonResponse>(Cancellation))!.Id;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient browser, string token, HttpMethod method, string url, object body)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-XSRF-TOKEN", token);
        return await browser.SendAsync(request, Cancellation);
    }
}
