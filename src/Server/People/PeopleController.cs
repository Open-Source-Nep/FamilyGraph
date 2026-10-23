using FamilyGraph.Application.People;
using FamilyGraph.Business.People;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Server.Accounts;

namespace Server.People;

/// <summary>
/// Person profiles (PRD 01, PRD 05, #12, #18, #74): create a profile, view one, rename one you
/// maintain, make it Public or Private or set its image, delete one, and list the profiles you maintain. A profile is never an account,
/// and creating one never links it to the signed-in user's identity.
/// </summary>
[ApiController]
[Authorize]
[Route("api/people")]
public sealed class PeopleController(PersonService people) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PersonResponse>> Create(PersonNameRequest request, CancellationToken cancellationToken)
    {
        var id = await people.CreateAsync(request.ToName(), User.AccountId(), cancellationToken);
        var person = await people.GetAsync(id, User.Viewer(), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = id.Value }, PersonResponse.From(person));
    }

    /// <summary>A Private profile is a <c>Private</c> placeholder to all but its maintainer and administrators (PRD 05).</summary>
    [HttpGet("{id:guid}")]
    public async Task<PersonResponse> Get(Guid id, CancellationToken cancellationToken) =>
        PersonResponse.From(await people.GetAsync(ToPersonId(id), User.Viewer(), cancellationToken));

    /// <summary>Renames the person. Only the profile's maintainer may (403 <c>person.not_maintainer</c>).</summary>
    [HttpPut("{id:guid}/name")]
    public async Task<IActionResult> Rename(Guid id, PersonNameRequest request, CancellationToken cancellationToken)
    {
        await people.RenameAsync(ToPersonId(id), request.ToName(), User.AccountId(), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Makes the profile Public or Private. Only the profile's maintainer may (403
    /// <c>person.not_maintainer</c>); setting the current value again is 409 <c>person.visibility_unchanged</c>.
    /// </summary>
    [HttpPut("{id:guid}/visibility")]
    public async Task<IActionResult> SetVisibility(Guid id, VisibilityRequest request, CancellationToken cancellationToken)
    {
        await people.SetVisibilityAsync(ToPersonId(id), request.ToVisibility(), User.AccountId(), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Deletes the profile for good, leaving a <c>Deleted</c> placeholder (PRD 05); 204. Its
    /// maintainer may, or an administrator (403 <c>person.not_maintainer</c> otherwise); a deleted
    /// profile is 409 <c>person.deleted</c>.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await people.DeleteAsync(ToPersonId(id), User.Viewer(), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Sets or replaces the profile's image from the raw request body: a JPEG, PNG or WebP of at
    /// most 1 MiB (D-028), which the Client has already resized. A larger body is not read past the limit. Only the maintainer may (403
    /// <c>person.not_maintainer</c>); 422 <c>profile_image.too_large</c> or
    /// <c>profile_image.unsupported_format</c>. Answers the new image's ID.
    /// </summary>
    [HttpPut("{id:guid}/image")]
    [Consumes("image/jpeg", "image/png", "image/webp")]
    public async Task<ImageResponse> SetImage(Guid id, CancellationToken cancellationToken)
    {
        var content = await ReadBodyAsync(cancellationToken);
        var imageId = await people.SetImageAsync(ToPersonId(id), content, User.AccountId(), cancellationToken);
        return new ImageResponse(imageId.Value);
    }

    /// <summary>Removes the profile's image; 204. Only the maintainer may; 409 <c>person.no_image</c> without one.</summary>
    [HttpDelete("{id:guid}/image")]
    public async Task<IActionResult> RemoveImage(Guid id, CancellationToken cancellationToken)
    {
        await people.RemoveImageAsync(ToPersonId(id), User.AccountId(), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// The profile's image, if the viewer sees the profile by name. A placeholder never shows one,
    /// so it answers 404 <c>profile_image.not_found</c> like a profile without an image (PRD 05).
    /// Browsers revalidate every time, so a profile made Private stops showing its image at once.
    /// </summary>
    [HttpGet("{id:guid}/image")]
    public async Task<IActionResult> GetImage(Guid id, CancellationToken cancellationToken)
    {
        var image = await people.GetImageAsync(ToPersonId(id), User.Viewer(), cancellationToken);
        Response.Headers.CacheControl = "private, no-cache";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(image.Content, image.Format.MediaType, lastModified: null, new EntityTagHeaderValue($"\"{image.Id.Value}\""));
    }

    [HttpGet("maintained")]
    public async Task<ProfileListResponse> ListMaintained(CancellationToken cancellationToken) =>
        ProfileListResponse.From(await people.ListMaintainedAsync(User.AccountId(), cancellationToken));

    /// <summary>
    /// Reads at most one byte more than an image may have, so a larger body is never buffered and
    /// the domain refuses it as too large.
    /// </summary>
    private async Task<byte[]> ReadBodyAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[ProfileImage.MaxBytes + 1];
        var length = 0;
        int read;
        while (length < buffer.Length
            && (read = await Request.Body.ReadAsync(buffer.AsMemory(length), cancellationToken)) > 0)
        {
            length += read;
        }

        return buffer[..length];
    }

    /// <summary>The empty GUID is never a person, so it is reported like any other unknown ID.</summary>
    private static PersonId ToPersonId(Guid id) =>
        id == Guid.Empty ? throw new PersonNotFoundException() : new PersonId(id);
}
