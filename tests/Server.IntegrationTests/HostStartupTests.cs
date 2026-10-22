using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Server.IntegrationTests.Accounts;

namespace Server.IntegrationTests;

public class HostStartupTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Server_starts_and_serves_the_OpenAPI_document_in_development()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        Assert.True(response.IsSuccessStatusCode, $"Unexpected status {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Sign_in_reports_missing_Google_configuration_even_without_a_database()
    {
        // Cleared explicitly, since a developer's appsettings.Development.json may configure it.
        using var unconfigured = factory.WithWebHostBuilder(builder => builder.UseSetting("Authentication:Google:ClientId", ""));
        using var client = unconfigured.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        var cancellation = TestContext.Current.CancellationToken;
        using var account = await client.GetAsync("/api/account", cancellation);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/account/google-sign-in")
        {
            Content = JsonContent.Create(new { idToken = "token", nonceSecret = "secret" }),
        };
        request.Headers.Add("X-XSRF-TOKEN", AccountTestFactory.XsrfToken(account));
        using var response = await client.SendAsync(request, cancellation);
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(cancellation);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("account.sign_in_unavailable", problem!["code"].ToString());
    }
}
