using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Server.IntegrationTests.Accounts;
using Server.People;

namespace Server.IntegrationTests.People;

public class ProfileImageEndpointTests(AccountTestFactory factory) : IClassFixture<AccountTestFactory>
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("AC", "AC-01.18")]
    public async Task Maintainer_uploads_an_image_that_another_user_can_load()
    {
        var (browser, token) = await SignInAsync("maya@example.org");
        var id = await CreateAsync(browser, token, "Maya");

        using var upload = await PutImageAsync(browser, token, id, Jpeg, "image/jpeg");
        var imageId = (await upload.Content.ReadFromJsonAsync<ImageResponse>(Cancellation))!.ImageId;

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var (other, _) = await SignInAsync("gita@example.org");
        var person = await other.GetFromJsonAsync<PersonResponse>($"/api/people/{id}", Cancellation);
        Assert.Equal(imageId, person!.ImageId);
        using var image = await other.GetAsync($"/api/people/{id}/image?v={imageId}", Cancellation);
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/jpeg", image.Content.Headers.ContentType!.MediaType);
        Assert.Equal(Jpeg, await image.Content.ReadAsByteArrayAsync(Cancellation));
        Assert.Equal($"\"{imageId}\"", image.Headers.ETag!.Tag);
        Assert.True(image.Headers.CacheControl!.Private && image.Headers.CacheControl.NoCache);
        Assert.Equal(["nosniff"], image.Headers.GetValues("X-Content-Type-Options"));
    }

    [Fact]
    [Trait("AC", "AC-01.18")]
    public async Task Someone_other_than_the_maintainer_cannot_set_or_remove_the_image()
    {
        var (browser, token) = await SignInAsync("maya@example.org");
        var id = await CreateAsync(browser, token, "Maya");
        (await PutImageAsync(browser, token, id, Jpeg, "image/jpeg")).Dispose();
        var (other, otherToken) = await SignInAsync("gita@example.org");

        using var upload = await PutImageAsync(other, otherToken, id, Png, "image/png");
        using var remove = await SendAsync(other, otherToken, HttpMethod.Delete, $"/api/people/{id}/image");

        Assert.Equal((HttpStatusCode.Forbidden, HttpStatusCode.Forbidden), (upload.StatusCode, remove.StatusCode));
        Assert.Equal("person.not_maintainer", await CodeAsync(upload));
    }

    [Fact]
    [Trait("AC", "AC-01.18")]
    public async Task Maintainer_removes_the_image()
    {
        var (browser, token) = await SignInAsync("maya@example.org");
        var id = await CreateAsync(browser, token, "Maya");
        (await PutImageAsync(browser, token, id, Jpeg, "image/jpeg")).Dispose();

        using var remove = await SendAsync(browser, token, HttpMethod.Delete, $"/api/people/{id}/image");
        using var again = await SendAsync(browser, token, HttpMethod.Delete, $"/api/people/{id}/image");
        using var image = await browser.GetAsync($"/api/people/{id}/image", Cancellation);

        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
        Assert.Equal((HttpStatusCode.Conflict, "person.no_image"), (again.StatusCode, await CodeAsync(again)));
        Assert.Equal((HttpStatusCode.NotFound, "profile_image.not_found"), (image.StatusCode, await CodeAsync(image)));
    }

    [Fact]
    [Trait("AC", "AC-01.19")]
    public async Task Content_that_is_not_a_supported_image_is_refused()
    {
        var (browser, token) = await SignInAsync("maya@example.org");
        var id = await CreateAsync(browser, token, "Maya");

        using var gif = await PutImageAsync(browser, token, id, "GIF89a"u8.ToArray(), "image/jpeg");
        using var wrongType = await PutImageAsync(browser, token, id, Jpeg, "image/gif");

        Assert.Equal((HttpStatusCode.UnprocessableEntity, "profile_image.unsupported_format"), (gif.StatusCode, await CodeAsync(gif)));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, wrongType.StatusCode);
    }

    [Fact]
    [Trait("AC", "AC-01.19")]
    public async Task An_image_over_one_mebibyte_is_refused()
    {
        var (browser, token) = await SignInAsync("maya@example.org");
        var id = await CreateAsync(browser, token, "Maya");
        var content = new byte[(1024 * 1024) + 1];
        Jpeg.CopyTo(content, 0);

        using var response = await PutImageAsync(browser, token, id, content, "image/jpeg");

        Assert.Equal((HttpStatusCode.UnprocessableEntity, "profile_image.too_large"), (response.StatusCode, await CodeAsync(response)));
    }

    [Fact]
    [Trait("AC", "AC-05.13")]
    public async Task A_private_profile_image_is_not_found_for_others()
    {
        var (browser, token) = await SignInAsync("maya@example.org");
        var id = await CreateAsync(browser, token, "Maya");
        (await PutImageAsync(browser, token, id, Jpeg, "image/jpeg")).Dispose();
        (await SendAsync(browser, token, HttpMethod.Put, $"/api/people/{id}/visibility", new { visibility = "Private" })).Dispose();
        var (other, _) = await SignInAsync("gita@example.org");

        using var image = await other.GetAsync($"/api/people/{id}/image", Cancellation);
        var person = await other.GetFromJsonAsync<PersonResponse>($"/api/people/{id}", Cancellation);
        using var own = await browser.GetAsync($"/api/people/{id}/image", Cancellation);

        Assert.Equal((HttpStatusCode.NotFound, "profile_image.not_found"), (image.StatusCode, await CodeAsync(image)));
        Assert.Null(person!.ImageId);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
    }

    [Fact]
    public async Task Upload_requires_the_antiforgery_token()
    {
        var (browser, token) = await SignInAsync("maya@example.org");
        var id = await CreateAsync(browser, token, "Maya");
        using var content = new ByteArrayContent(Jpeg);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

        using var response = await browser.PutAsync($"/api/people/{id}/image", content, Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<(HttpClient Browser, string Token)> SignInAsync(string email)
    {
        var browser = factory.CreateBrowser();
        return (browser, await AccountTestFactory.SignInAsync(browser, email, Cancellation));
    }

    private static async Task<Guid> CreateAsync(HttpClient browser, string token, string firstName)
    {
        using var response = await SendAsync(browser, token, HttpMethod.Post, "/api/people", new { firstName });
        return (await response.Content.ReadFromJsonAsync<PersonResponse>(Cancellation))!.Id;
    }

    private static async Task<HttpResponseMessage> PutImageAsync(HttpClient browser, string token, Guid id, byte[] bytes, string mediaType)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/people/{id}/image") { Content = new ByteArrayContent(bytes) };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        request.Headers.Add("X-XSRF-TOKEN", token);
        return await browser.SendAsync(request, Cancellation);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient browser, string token, HttpMethod method, string url, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = body is null ? null : JsonContent.Create(body) };
        request.Headers.Add("X-XSRF-TOKEN", token);
        return await browser.SendAsync(request, Cancellation);
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(Cancellation))!["code"].ToString();
}
