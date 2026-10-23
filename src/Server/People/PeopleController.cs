using FamilyGraph.Application.People;
using FamilyGraph.Business.People;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Accounts;

namespace Server.People;

/// <summary>
/// Person profiles (PRD 01, PRD 05, #12, #18): create a profile, view one, rename one you maintain
/// or make it Public or Private, delete one, and list the profiles you maintain. A profile is never an account,
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

    [HttpGet("maintained")]
    public async Task<ProfileListResponse> ListMaintained(CancellationToken cancellationToken) =>
        ProfileListResponse.From(await people.ListMaintainedAsync(User.AccountId(), cancellationToken));

    /// <summary>The empty GUID is never a person, so it is reported like any other unknown ID.</summary>
    private static PersonId ToPersonId(Guid id) =>
        id == Guid.Empty ? throw new PersonNotFoundException() : new PersonId(id);
}
