using FamilyGraph.Application.People;
using FamilyGraph.Application.Relationships;
using FamilyGraph.Business.People;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Accounts;

namespace Server.Relationships;

/// <summary>
/// Parent–child links (PRD 02, #14): create a biological or adoptive link where you maintain at
/// least one endpoint, and list a person's parents and children through active links.
/// </summary>
[ApiController]
[Authorize]
[Route("api/relationships")]
public sealed class RelationshipsController(RelationshipService relationships) : ControllerBase
{
    /// <summary>
    /// Creates a link (201). An endpoint you do not maintain starts pending, so the link is not
    /// active until its maintainer approves (PRD 03). 403 <c>link.not_endpoint_maintainer</c>,
    /// 409 <c>link.duplicate</c>, 422 for self-links and cycles.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CreatedLinkResponse>> Create(CreateLinkRequest request, CancellationToken cancellationToken)
    {
        var link = await relationships.CreateLinkAsync(
            ToPersonId(request.ParentId!.Value),
            ToPersonId(request.ChildId!.Value),
            request.ToType(),
            User.AccountId(),
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, CreatedLinkResponse.From(link));
    }

    [HttpGet("/api/people/{id:guid}/relatives")]
    public async Task<DirectRelativesResponse> ListDirectRelatives(Guid id, CancellationToken cancellationToken) =>
        DirectRelativesResponse.From(
            await relationships.ListDirectRelativesAsync(ToPersonId(id), User.AccountId(), cancellationToken));

    /// <summary>The empty GUID is never a person, so it is reported like any other unknown ID.</summary>
    private static PersonId ToPersonId(Guid id) =>
        id == Guid.Empty ? throw new PersonNotFoundException() : new PersonId(id);
}
