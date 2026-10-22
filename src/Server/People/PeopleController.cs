using FamilyGraph.Application.People;
using FamilyGraph.Business.People;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Accounts;

namespace Server.People;

/// <summary>
/// Person profiles (PRD 01, #12): create a profile, view one, rename one you maintain, and list
/// the profiles you maintain. A profile is never an account, and creating one never links it to
/// the signed-in user's identity.
/// </summary>
[ApiController]
[Authorize]
[Route("api/people")]
public sealed class PeopleController(PersonService people) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PersonResponse>> Create(PersonNameRequest request, CancellationToken cancellationToken)
    {
        var accountId = User.AccountId();
        var id = await people.CreateAsync(request.ToName(), accountId, cancellationToken);
        var person = await people.GetAsync(id, accountId, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = id.Value }, PersonResponse.From(person));
    }

    [HttpGet("{id:guid}")]
    public async Task<PersonResponse> Get(Guid id, CancellationToken cancellationToken) =>
        PersonResponse.From(await people.GetAsync(ToPersonId(id), User.AccountId(), cancellationToken));

    /// <summary>Renames the person. Only the profile's maintainer may (403 <c>person.not_maintainer</c>).</summary>
    [HttpPut("{id:guid}/name")]
    public async Task<IActionResult> Rename(Guid id, PersonNameRequest request, CancellationToken cancellationToken)
    {
        await people.RenameAsync(ToPersonId(id), request.ToName(), User.AccountId(), cancellationToken);
        return NoContent();
    }

    [HttpGet("maintained")]
    public async Task<ProfileListResponse> ListMaintained(CancellationToken cancellationToken) =>
        ProfileListResponse.From(await people.ListMaintainedAsync(User.AccountId(), cancellationToken));

    /// <summary>The empty GUID is never a person, so it is reported like any other unknown ID.</summary>
    private static PersonId ToPersonId(Guid id) =>
        id == Guid.Empty ? throw new PersonNotFoundException() : new PersonId(id);
}
