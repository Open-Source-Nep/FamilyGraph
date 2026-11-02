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
        using var unconfigured = factory.WithWebHostBuilder(builder => builder.UseSetting("Authentication:Google:ClientId", ""));
        using var client = unconfigured.CreateClient();

        var configuration = await client.GetFromJsonAsync<ClientConfigurationResponse>("/api/configuration", Cancellation);

        Assert.False(configuration!.Google.Enabled);
        Assert.Null(configuration.Google.ClientId);
    }

    [Fact]
    public async Task Reports_Google_sign_in_as_available_with_its_public_client_ID()
    {
        using var configured = factory.WithWebHostBuilder(builder => builder
            .UseSetting("Authentication:Google:ClientId", "test-client-id"));
        using var client = configured.CreateClient();

        var configuration = await client.GetFromJsonAsync<ClientConfigurationResponse>("/api/configuration", Cancellation);

        Assert.True(configuration!.Google.Enabled);
        Assert.Equal("test-client-id", configuration.Google.ClientId);
    }

    [Fact]
    [Trait("AC", "AC-08.32")]
    public async Task Lists_the_cultures_with_their_languages_and_the_default_one()
    {
        using var configured = factory.WithWebHostBuilder(builder => builder.UseSetting("Cultures:Default", "ne"));
        using var client = configured.CreateClient();

        var configuration = await client.GetFromJsonAsync<ClientConfigurationResponse>("/api/configuration", Cancellation);

        Assert.Equal("ne", configuration!.Cultures.Default);
        Assert.Equal(
            [("en", "English", "en"), ("ne", "नेपाली", "ne,en")],
            configuration.Cultures.Available.Select(culture => (culture.Code, culture.Name[culture.Languages[0]], string.Join(",", culture.Languages))));
        Assert.Equal("अङ्ग्रेजी", configuration.Cultures.Available[0].Name["ne"]);
    }
}
