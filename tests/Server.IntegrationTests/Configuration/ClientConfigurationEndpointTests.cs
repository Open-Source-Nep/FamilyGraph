using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Server.Configuration;

namespace Server.IntegrationTests.Configuration;

public class ClientConfigurationEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Reports_Google_sign_in_as_unavailable_when_it_is_not_configured()
    {
        using var client = factory.CreateClient();

        var configuration = await client.GetFromJsonAsync<ClientConfigurationResponse>("/api/configuration", Cancellation);

        Assert.False(configuration!.Google.Enabled);
    }

    [Fact]
    public async Task Reports_Google_sign_in_as_available_without_exposing_its_credentials()
    {
        using var configured = factory.WithWebHostBuilder(builder => builder
            .UseSetting("Authentication:Google:ClientId", "test-client-id")
            .UseSetting("Authentication:Google:ClientSecret", "test-client-secret"));
        using var client = configured.CreateClient();

        var body = await client.GetStringAsync("/api/configuration", Cancellation);
        var configuration = await client.GetFromJsonAsync<ClientConfigurationResponse>("/api/configuration", Cancellation);

        Assert.True(configuration!.Google.Enabled);
        Assert.DoesNotContain("test-client-id", body, StringComparison.Ordinal);
        Assert.DoesNotContain("test-client-secret", body, StringComparison.Ordinal);
    }
}
