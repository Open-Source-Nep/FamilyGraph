using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FamilyGraph.Business.Export;
using Server.Accounts;
using Server.IntegrationTests.Accounts;
using Server.People;

namespace Server.IntegrationTests.Export;

/// <summary>An administrator's download of the whole instance as a zip (D-029, #75; D-049, #124).</summary>
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
    public async Task Administrator_downloads_a_zip_of_accounts_profiles_links_images_and_history_and_the_export_is_recorded()
    {
        var email = UniqueEmail();
        var (owner, token) = await SignInAsync(email);
        var parent = await CreatePersonAsync(owner, token, "Hari", "Sharma");
        var child = await CreatePersonAsync(owner, token, "Maya");
        using var hidden = await SendAsync(owner, token, HttpMethod.Put, $"/api/people/{child}/visibility", new { visibility = "Private" });
        using var link = await SendAsync(owner, token, HttpMethod.Post, "/api/relationships", new { parentId = parent, childId = child, type = "Adoptive" });
        using (var photo = new HttpRequestMessage(HttpMethod.Put, $"/api/people/{parent}/image") { Content = new ByteArrayContent(Jpeg) })
        {
            photo.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            photo.Headers.Add("X-XSRF-TOKEN", token);
            (await owner.SendAsync(photo, Cancellation)).EnsureSuccessStatusCode();
        }

        var (admin, _) = await SignInAsync(AccountTestFactory.AdministratorEmail);

        using var response = await admin.GetAsync("/api/export", Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        Assert.EndsWith(".zip", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var (file, zip) = await ReadAsync(response);
        Assert.Equal("familygraph-export", file.GetProperty("format").GetString());
        Assert.Equal(2, file.GetProperty("version").GetInt32());

        var account = Single(file, "accounts", "email", email);
        Assert.False(string.IsNullOrEmpty(account.GetProperty("googleId").GetString()));
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
        var image = Single(file, "images", "personId", parent.ToString());
        Assert.Equal(image.GetProperty("id").GetGuid(), Single(file, "people", "id", parent.ToString()).GetProperty("imageId").GetGuid());
        var entry = zip.GetEntry(image.GetProperty("file").GetString()!);
        Assert.NotNull(entry);
        await using (var content = await entry.OpenAsync(Cancellation))
        using (var bytes = new MemoryStream())
        {
            await content.CopyToAsync(bytes, Cancellation);
            Assert.Equal(Jpeg, bytes.ToArray());
        }

        var adminAccount = await admin.GetFromJsonAsync<AccountResponse>("/api/account", Cancellation);
        Assert.Contains(factory.People.AuditEvents, entry => entry.Event is DataExported e && e.ActorId.Value == adminAccount!.Id);
        Assert.Contains(
            file.GetProperty("auditEvents").EnumerateArray(),
            row => row.GetProperty("type").GetString() == nameof(DataExported) && row.GetProperty("actorId").GetGuid() == adminAccount!.Id);
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

        using var response = await admin.GetAsync("/api/export", Cancellation);
        var (file, _) = await ReadAsync(response);
        var body = file.GetRawText();

        Assert.DoesNotContain("Zebediah", body, StringComparison.Ordinal);
        Assert.DoesNotContain("[erased]", body, StringComparison.Ordinal);
        var person = Single(file, "people", "id", deleted.ToString());
        Assert.Equal(JsonValueKind.Null, person.GetProperty("firstName").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, person.GetProperty("deletedAt").ValueKind);
    }

    /// <summary>A JPEG's first bytes, which is all the format check reads.</summary>
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 1, 2, 3];

    /// <summary>The zip's JSON and the zip itself, to read images from.</summary>
    internal static async Task<(JsonElement File, ZipArchive Zip)> ReadAsync(HttpResponseMessage response)
    {
        var bytes = new MemoryStream(await response.Content.ReadAsByteArrayAsync(Cancellation));
        var zip = new ZipArchive(bytes, ZipArchiveMode.Read);
        await using var json = await zip.GetEntry("familygraph.json")!.OpenAsync(Cancellation);
        return ((await JsonDocument.ParseAsync(json, cancellationToken: Cancellation)).RootElement.Clone(), zip);
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
