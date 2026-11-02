using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Server.Configuration;
using Server.Localization;

namespace Server.IntegrationTests.Localization;

public class TranslationsEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("AC", "AC-09.13")]
    public async Task Serves_the_Nepali_translations_of_the_Client()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/translations/ne", Cancellation);
        var file = await response.Content.ReadFromJsonAsync<TranslationsResponse>(Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());
        Assert.Equal("ne", file!.Locale);
        Assert.Equal("मानिसहरू", file.Translations["header.people"]);
    }

    [Fact]
    [Trait("AC", "AC-09.13")]
    public async Task Answers_not_modified_while_the_translations_are_unchanged()
    {
        using var client = factory.CreateClient();
        using var first = await client.GetAsync("/api/translations/ne", Cancellation);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/translations/ne");
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        using var second = await client.SendAsync(request, Cancellation);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }

    [Theory]
    [Trait("AC", "AC-09.13")]
    [InlineData("en")]
    [InlineData("fr")]
    [InlineData("..%2Fappsettings")]
    public async Task Has_no_translations_for_English_or_unknown_languages(string code)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/translations/{code}", Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    [Trait("AC", "AC-09.14")]
    public async Task Offers_English_first_and_starts_in_English_by_default()
    {
        using var client = factory.CreateClient();

        var configuration = await client.GetFromJsonAsync<ClientConfigurationResponse>("/api/configuration", Cancellation);

        Assert.Equal("en", configuration!.Languages.Default);
        Assert.Equal([new Language("en", "English"), new Language("ne", "नेपाली")], configuration.Languages.Available);
    }

    [Fact]
    [Trait("AC", "AC-09.14")]
    public async Task Starts_in_the_language_the_instance_configures()
    {
        using var nepali = factory.WithWebHostBuilder(builder => builder.UseSetting("Languages:Default", "ne"));
        using var client = nepali.CreateClient();

        var configuration = await client.GetFromJsonAsync<ClientConfigurationResponse>("/api/configuration", Cancellation);

        Assert.Equal("ne", configuration!.Languages.Default);
    }

    [Fact]
    [Trait("AC", "AC-09.14")]
    public void Refuses_to_start_with_a_default_language_it_has_no_translations_for()
    {
        using var unknown = factory.WithWebHostBuilder(builder => builder.UseSetting("Languages:Default", "fr"));

        var error = Assert.Throws<InvalidOperationException>(() => unknown.CreateClient());

        Assert.Contains("Languages:Default", error.Message);
    }
}
