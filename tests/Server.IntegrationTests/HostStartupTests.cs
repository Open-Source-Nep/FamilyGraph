using Microsoft.AspNetCore.Mvc.Testing;

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
}
