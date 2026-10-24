using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Server.IntegrationTests;

/// <summary>The Server serves the built Client from its web root (D-011).</summary>
public sealed class ClientHostingTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string IndexHtml = "<!doctype html><title>FamilyGraph</title>";

    private readonly string _webRoot = Directory.CreateTempSubdirectory("familygraph-wwwroot-").FullName;
    private readonly WebApplicationFactory<Program> _factory;

    public ClientHostingTests(WebApplicationFactory<Program> factory)
    {
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), IndexHtml);
        File.WriteAllText(Path.Combine(_webRoot, "main.js"), "console.log('client');");
        File.WriteAllText(Path.Combine(_webRoot, "chunk-BMu6qV-U.js"), "export {};");
        _factory = factory.WithWebHostBuilder(builder => builder.UseWebRoot(_webRoot));
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [Trait("AC", "AC-09.3")]
    [InlineData("/")]
    [InlineData("/people/0b8f6c1e-2f43-4c55-9d1a-6f0f1a7f2c11/graph")]
    public async Task Client_routes_are_served_the_client_index_page(string path)
    {
        using var client = _factory.CreateClient();

        var body = await client.GetStringAsync(path, Cancellation);

        Assert.Equal(IndexHtml, body);
    }

    [Fact]
    [Trait("AC", "AC-09.3")]
    public async Task Client_files_are_served_as_static_files()
    {
        using var client = _factory.CreateClient();

        var body = await client.GetStringAsync("/main.js", Cancellation);

        Assert.Equal("console.log('client');", body);
    }

    [Fact]
    [Trait("AC", "AC-09.3")]
    public async Task Unknown_API_paths_are_not_found_instead_of_the_index_page()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/no-such-endpoint", Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [Trait("AC", "AC-09.3")]
    [InlineData("/")]
    [InlineData("/index.html")]
    [InlineData("/people")]
    [InlineData("/main.js")]
    public async Task The_index_page_and_unhashed_files_are_revalidated_on_every_load(string path)
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(path, Cancellation);

        Assert.True(response.Headers.CacheControl!.NoCache);
    }

    [Fact]
    [Trait("AC", "AC-09.3")]
    public async Task Hashed_build_files_are_cached_for_good()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/chunk-BMu6qV-U.js", Cancellation);

        Assert.Equal("public, max-age=31536000, immutable", response.Headers.CacheControl!.ToString());
    }

    [Fact]
    [Trait("AC", "AC-09.3")]
    public async Task A_removed_chunk_is_not_found_instead_of_the_index_page()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/chunk-REMOVED_.js", Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public void Dispose()
    {
        _factory.Dispose();
        Directory.Delete(_webRoot, recursive: true);
    }
}
