using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FamilyGraph.Business.Import;
using Server.Import;
using Server.IntegrationTests.Accounts;
using Server.People;
using Server.Relationships;

namespace Server.IntegrationTests.Import;

/// <summary>
/// An administrator's upload of an export file into an empty instance (D-033, #87). Each test runs
/// its own Server, since an import needs an instance without profiles.
/// </summary>
public class ImportEndpointTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Import_requires_a_session()
    {
        await using var factory = new AccountTestFactory();
        using var browser = factory.CreateBrowser();

        using var response = await browser.PostAsync("/api/import", Json("{}"), Cancellation);

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
    public async Task An_export_is_imported_into_an_empty_instance_with_matching_maintainers_and_only_once()
    {
        string file;
        Guid hari, maya;
        await using (var source = new AccountTestFactory())
        {
            var (owner, ownerToken) = await SignInAsync(source, "bina@example.org");
            hari = await CreatePersonAsync(owner, ownerToken, "Hari");
            maya = await CreatePersonAsync(owner, ownerToken, "Maya");
            (await SendAsync(owner, ownerToken, HttpMethod.Post, "/api/relationships", new { parentId = hari, childId = maya, type = "Adoptive" })).Dispose();
            var (other, otherToken) = await SignInAsync(source, "gone@example.org");
            await CreatePersonAsync(other, otherToken, "Sita");
            var (sourceAdmin, _) = await SignInAsync(source, AccountTestFactory.AdministratorEmail);
            file = await sourceAdmin.GetStringAsync("/api/export", Cancellation);
        }

        await using var target = new AccountTestFactory();
        var (bina, _) = await SignInAsync(target, "bina@example.org");
        var (admin, adminToken) = await SignInAsync(target, AccountTestFactory.AdministratorEmail);

        using var response = await PostAsync(admin, adminToken, file);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            new ImportResponse(People: 3, Links: 1, Marriages: 0, MatchedAccounts: 1, AssignedToYou: 1),
            await response.Content.ReadFromJsonAsync<ImportResponse>(Cancellation));
        var maintained = await bina.GetFromJsonAsync<ProfileListResponse>("/api/people/maintained", Cancellation);
        Assert.Equal(new HashSet<string> { "Hari", "Maya" }, maintained!.People.Select(person => person.FirstName).ToHashSet());
        var relatives = await bina.GetFromJsonAsync<DirectRelativesResponse>($"/api/people/{maya}/relatives", Cancellation);
        var parent = Assert.Single(relatives!.Parents);
        Assert.Equal((hari, "Adoptive"), (parent.Id, parent.Type));
        Assert.Single(target.People.AuditEvents, entry => entry.Event is DataImported);

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
    [InlineData("""{ "format": "something-else", "version": 1 }""", "import.unsupported_format")]
    [InlineData("""{ "format": "familygraph-export", "version": 2 }""", "import.unsupported_version")]
    public async Task A_file_that_is_not_a_version_1_export_is_refused(string body, string code)
    {
        await using var factory = new AccountTestFactory();
        var (admin, token) = await SignInAsync(factory, AccountTestFactory.AdministratorEmail);

        using var response = await PostAsync(admin, token, body);

        var problem = await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "import.invalid_file");
        Assert.Equal(code, Assert.Single(problem.GetProperty("problems").EnumerateArray()).GetProperty("code").GetString());
    }

    private static object Person(Guid id, string firstName) =>
        new { id, firstName, visibility = "Public", maintainerId = Guid.NewGuid() };

    private static string File(object[]? people = null, object[]? links = null) => JsonSerializer.Serialize(new
    {
        format = "familygraph-export",
        version = 1,
        exportedAt = DateTimeOffset.UtcNow,
        accounts = Array.Empty<object>(),
        people = people ?? [],
        links = links ?? [],
        marriages = Array.Empty<object>(),
    });

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal(code, problem.GetProperty("code").GetString());
        return problem;
    }

    private static async Task<(HttpClient Browser, string Token)> SignInAsync(AccountTestFactory factory, string email)
    {
        var browser = factory.CreateBrowser();
        return (browser, await AccountTestFactory.SignInAsync(browser, email, Cancellation));
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient browser, string token, string body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/import") { Content = Json(body) };
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
