using FamilyGraph.Application.People;
using FamilyGraph.Application.Relationships;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Accounts;

namespace Server.Relationships;

/// <summary>
/// Parent–child links (PRD 02, PRD 03, #14, #59, #61): create a biological or adoptive link where
/// you maintain at least one endpoint, approve or disagree for an endpoint you maintain, delete a
/// link whose two people you maintain, list the inactive links of your profiles, and list a
/// person's parents and children through active links.
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

    /// <summary>
    /// The non-deleted, inactive links with an endpoint you maintain, those waiting for your
    /// approval first (D-021).
    /// </summary>
    [HttpGet("inactive")]
    public async Task<LinkConsentListResponse> ListInactive(CancellationToken cancellationToken) =>
        LinkConsentListResponse.From(await relationships.ListInactiveLinksAsync(User.Viewer(), cancellationToken));

    /// <summary>
    /// Approves the link for the endpoint you maintain, from pending or after a disagreement
    /// (D-013). 403 <c>link.consent_not_maintainer</c>, 404 <c>link.not_found</c>, 409
    /// <c>link.consent_unchanged</c>, 422 when reactivating would form a cycle.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<LinkConsentResponse> Approve(Guid id, LinkConsentRequest request, CancellationToken cancellationToken) =>
        LinkConsentResponse.From(await relationships.ApproveAsync(
            ToLinkId(id), request.ToEndpoint(), User.Viewer(), cancellationToken));

    /// <summary>
    /// Declines a pending endpoint you maintain or withdraws your approval (D-013); the link is
    /// inactive at once. 403 <c>link.consent_not_maintainer</c>, 404 <c>link.not_found</c>, 409
    /// <c>link.consent_unchanged</c>, 422 <c>link.sole_maintainer_must_delete</c>.
    /// </summary>
    [HttpPost("{id:guid}/disagree")]
    public async Task<LinkConsentResponse> Disagree(Guid id, LinkConsentRequest request, CancellationToken cancellationToken) =>
        LinkConsentResponse.From(await relationships.DisagreeAsync(
            ToLinkId(id), request.ToEndpoint(), User.Viewer(), cancellationToken));

    /// <summary>
    /// Deletes the link, in any consent state, when you maintain both people (D-014); 204. Anyone
    /// else withdraws their approval instead. 403 <c>link.not_sole_maintainer</c>, 404
    /// <c>link.not_found</c>, 409 <c>link.deleted</c>.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<NoContentResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await relationships.DeleteAsync(ToLinkId(id), User.AccountId(), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Numbers the parent's listed children 1, 2, 3… in birth order (D-016, D-037). Only the parent's
    /// maintainer may (403 <c>link.birth_order_not_parent_maintainer</c>); a link that is not this
    /// parent's is 404 <c>link.not_found</c>, a repeated one 422 <c>link.birth_order_invalid</c>.
    /// </summary>
    [HttpPut("/api/people/{id:guid}/children/order")]
    public async Task<NoContentResult> OrderChildren(Guid id, ChildrenOrderRequest request, CancellationToken cancellationToken)
    {
        await relationships.OrderChildrenAsync(
            ToPersonId(id), [.. request.LinkIds!.Select(ToLinkId)], User.AccountId(), cancellationToken);
        return NoContent();
    }

    [HttpGet("/api/people/{id:guid}/relatives")]
    public async Task<DirectRelativesResponse> ListDirectRelatives(Guid id, CancellationToken cancellationToken) =>
        DirectRelativesResponse.From(
            await relationships.ListDirectRelativesAsync(ToPersonId(id), User.Viewer(), cancellationToken));

    /// <summary>The empty GUID is never a person, so it is reported like any other unknown ID.</summary>
    private static PersonId ToPersonId(Guid id) =>
        id == Guid.Empty ? throw new PersonNotFoundException() : new PersonId(id);

    private static LinkId ToLinkId(Guid id) =>
        id == Guid.Empty ? throw new LinkNotFoundException() : new LinkId(id);
}
