using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FamilyGraph.Business.Import;
using Server.Accounts;
using Server.Import;
using Server.IntegrationTests.Accounts;
using Server.People;
using Server.Relationships;

namespace Server.IntegrationTests.Import;

/// <summary>
/// An administrator's upload of an export zip into an empty instance (D-033, #87; D-049, #124). Each
/// test runs its own Server, since an import needs an instance without profiles.
/// </summary>
public class ImportEndpointTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Import_requires_a_session()
    {
        await using var factory = new AccountTestFactory();
        using var browser = factory.CreateBrowser();

        using var response = await browser.PostAsync("/api/import", ZipContent(File()), Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("AC", "AC-09.9")]
    public async Task A_user_who_is_not_an_administrator_cannot_import()
    {
        await using var factory = new AccountTestFactory();
        var (browser, token) = await SignInAsync(factory, "bina@example.org");

        using var response = await PostAsync(browser, token, File([]));

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "import.not_administrator");
        Assert.Empty(factory.People.AuditEvents);
    }

    [Fact]
    [Trait("AC", "AC-09.8")]
    [Trait("AC", "AC-09.9")]
    [Trait("AC", "AC-09.10")]
    [Trait("AC", "AC-09.12")]
    public async Task An_export_is_imported_into_an_empty_instance_with_its_accounts_and_only_once()
    {
        byte[] file;
        Guid hari, maya, binaId;
        await using (var source = new AccountTestFactory())
        {
            var (owner, ownerToken) = await SignInAsync(source, "bina@example.org", "google-bina");
            binaId = (await owner.GetFromJsonAsync<AccountResponse>("/api/account", Cancellation))!.Id;
            hari = await CreatePersonAsync(owner, ownerToken, "Hari");
            maya = await CreatePersonAsync(owner, ownerToken, "Maya");
            (await SendAsync(owner, ownerToken, HttpMethod.Post, "/api/relationships", new { parentId = hari, childId = maya, type = "Adoptive" })).Dispose();
            using (var photo = new HttpRequestMessage(HttpMethod.Put, $"/api/people/{maya}/image") { Content = new ByteArrayContent(Jpeg) })
            {
                photo.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                photo.Headers.Add("X-XSRF-TOKEN", ownerToken);
                (await owner.SendAsync(photo, Cancellation)).EnsureSuccessStatusCode();
            }

            var (other, otherToken) = await SignInAsync(source, "gone@example.org");
            await CreatePersonAsync(other, otherToken, "Sita");
            var (sourceAdmin, _) = await SignInAsync(source, AccountTestFactory.AdministratorEmail, "google-admin");
            file = await sourceAdmin.GetByteArrayAsync("/api/export", Cancellation);
        }

        await using var target = new AccountTestFactory();
        var (admin, adminToken) = await SignInAsync(target, AccountTestFactory.AdministratorEmail, "google-admin");

        using var response = await PostAsync(admin, adminToken, file);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ImportResponse>(Cancellation);
        Assert.Equal((3, 1, 0, 1, 0, 2, 1), (result!.People, result.Links, result.Marriages, result.Images, result.Handovers, result.CreatedAccounts, result.ReusedAccounts));
        Assert.True(result.AuditEvents > 0);
        Assert.Equal(result.AuditEvents, target.Import.RestoredAuditEvents.Count);
        Assert.Single(target.People.AuditEvents, entry => entry.Event is DataImported);

        // Bina's account came back with its ID, so her Google sign-in finds it and her profiles.
        var (bina, _) = await SignInAsync(target, "bina@example.org", "google-bina");
        Assert.Equal(binaId, (await bina.GetFromJsonAsync<AccountResponse>("/api/account", Cancellation))!.Id);
        var maintained = await bina.GetFromJsonAsync<ProfileListResponse>("/api/people/maintained", Cancellation);
        Assert.Equal(new HashSet<string> { "Hari", "Maya" }, maintained!.People.Select(person => person.FirstName).ToHashSet());
        var relatives = await bina.GetFromJsonAsync<DirectRelativesResponse>($"/api/people/{maya}/relatives", Cancellation);
        var parent = Assert.Single(relatives!.Parents);
        Assert.Equal((hari, "Adoptive"), (parent.Id, parent.Type));
        Assert.Equal(Jpeg, await bina.GetByteArrayAsync($"/api/people/{maya}/image", Cancellation));

        using var again = await PostAsync(admin, adminToken, file);

        await AssertProblemAsync(again, HttpStatusCode.Conflict, "import.instance_not_empty");
    }

    [Fact]
    [Trait("AC", "AC-09.11")]
    public async Task A_file_with_problems_is_refused_whole_with_each_problem_and_its_place()
    {
        await using var factory = new AccountTestFactory();
        var (admin, token) = await SignInAsync(factory, AccountTestFactory.AdministratorEmail);
        var parent = Guid.NewGuid();
        var body = File(
            people: [Person(parent, "Hari"), Person(Guid.NewGuid(), "")],
            links: [new { id = Guid.NewGuid(), parentId = parent, childId = Guid.NewGuid(), type = "Biological", parentApproval = "Approved", childApproval = "Approved" }]);

        using var response = await PostAsync(admin, token, body);

        var problem = await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "import.invalid_file");
        Assert.Equal(
            [("people[1]", "person.first_name_required"), ("links[0]", "import.unknown_person")],
            problem.GetProperty("problems").EnumerateArray().Select(item => (item.GetProperty("path").GetString(), item.GetProperty("code").GetString())));
        Assert.False(problem.GetProperty("moreProblems").GetBoolean());
        Assert.Empty(factory.People.People);
    }

    [Theory]
    [Trait("AC", "AC-09.11")]
    [InlineData("not json", "import.invalid_json")]
    [InlineData("""{ "people": [{ "id": 5 }] }""", "import.invalid_json")]
    [InlineData("""{ "format": "something-else", "version": 2 }""", "import.unsupported_format")]
    [InlineData("""{ "format": "familygraph-export", "version": 1 }""", "import.unsupported_version")]
    public async Task A_zip_that_is_not_a_version_2_export_is_refused(string json, string code)
    {
        await using var factory = new AccountTestFactory();
        var (admin, token) = await SignInAsync(factory, AccountTestFactory.AdministratorEmail);

        using var response = await PostAsync(admin, token, Zip(json));

        var problem = await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "import.invalid_file");
        Assert.Equal(code, Assert.Single(problem.GetProperty("problems").EnumerateArray()).GetProperty("code").GetString());
    }

    [Fact]
    [Trait("AC", "AC-09.11")]
    public async Task A_file_that_is_not_a_zip_or_has_no_export_data_is_refused()
    {
        await using var factory = new AccountTestFactory();
        var (admin, token) = await SignInAsync(factory, AccountTestFactory.AdministratorEmail);

        using var notZip = await PostAsync(admin, token, Encoding.UTF8.GetBytes("""{ "format": "familygraph-export", "version": 1 }"""));
        using var empty = await PostAsync(admin, token, Zip(null));

        foreach (var response in new[] { notZip, empty })
        {
            var problem = await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "import.invalid_file");
            Assert.Equal("import.unsupported_format", Assert.Single(problem.GetProperty("problems").EnumerateArray()).GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task A_zip_larger_than_the_setting_is_refused()
    {
        await using var factory = new AccountTestFactory();
        await using var limited = factory.WithWebHostBuilder(builder => builder.UseSetting("Import:MaxFileSizeMiB", "1"));
        var browser = limited.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var token = await AccountTestFactory.SignInAsync(browser, AccountTestFactory.AdministratorEmail, Cancellation);

        using var response = await PostAsync(browser, token, new byte[(1024 * 1024) + 1]);

        await AssertProblemAsync(response, HttpStatusCode.RequestEntityTooLarge, "import.too_large");
    }

    private static readonly Guid FileAccount = Guid.NewGuid();

    /// <summary>A JPEG's first bytes, which is all the format check reads.</summary>
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 1, 2, 3];

    private static object Person(Guid id, string firstName) =>
        new { id, firstName, visibility = "Public", maintainerId = FileAccount };

    private static byte[] File(object[]? people = null, object[]? links = null) => Zip(JsonSerializer.Serialize(new
    {
        format = "familygraph-export",
        version = 2,
        exportedAt = DateTimeOffset.UtcNow,
        accounts = new object[] { new { id = FileAccount, googleId = "google-file", email = "file@example.org", emailVerified = true } },
        people = people ?? [],
        links = links ?? [],
        marriages = Array.Empty<object>(),
    }));

    /// <summary>A zip with <paramref name="json"/> as its export data, or with no entry at all.</summary>
    private static byte[] Zip(string? json)
    {
        using var bytes = new MemoryStream();
        using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, leaveOpen: true))
        {
            if (json is not null)
            {
                using var entry = new StreamWriter(zip.CreateEntry("familygraph.json").Open());
                entry.Write(json);
            }
        }

        return bytes.ToArray();
    }

    private static ByteArrayContent ZipContent(byte[] body)
    {
        var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        return content;
    }

    private static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal(code, problem.GetProperty("code").GetString());
        return problem;
    }

    private static async Task<(HttpClient Browser, string Token)> SignInAsync(AccountTestFactory factory, string email, string? subject = null)
    {
        var browser = factory.CreateBrowser();
        return (browser, await AccountTestFactory.SignInAsync(browser, email, Cancellation, subject));
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient browser, string token, byte[] body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/import") { Content = ZipContent(body) };
        request.Headers.Add("X-XSRF-TOKEN", token);
        return await browser.SendAsync(request, Cancellation);
    }

    private static async Task<Guid> CreatePersonAsync(HttpClient browser, string token, string firstName)
    {
        using var response = await SendAsync(browser, token, HttpMethod.Post, "/api/people", new { firstName });
        return (await response.Content.ReadFromJsonAsync<PersonResponse>(Cancellation))!.Id;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient browser, string token, HttpMethod method, string url, object body)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-XSRF-TOKEN", token);
        return await browser.SendAsync(request, Cancellation);
    }
}
