using FamilyGraph.Application.People;
using FamilyGraph.Application.Relationships;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Accounts;

namespace Server.Relationships;

/// <summary>
/// Marriages (D-031): create one where you maintain at least one spouse, approve or disagree for a
/// spouse you maintain, mark it former or current, delete one whose two spouses you maintain, list
/// the inactive marriages of your profiles, and list a person's spouses. Consent works as for
/// parent–child links (PRD 03).
/// </summary>
[ApiController]
[Authorize]
[Route("api/marriages")]
public sealed class MarriagesController(MarriageService marriages) : ControllerBase
{
    /// <summary>
    /// Creates a marriage (201). A spouse you do not maintain starts pending, so the marriage is
    /// not active until their maintainer approves. 403 <c>marriage.not_spouse_maintainer</c>, 409
    /// <c>marriage.duplicate</c> or <c>marriage.person_deleted</c>, 422 <c>marriage.self</c>.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CreatedMarriageResponse>> Create(CreateMarriageRequest request, CancellationToken cancellationToken)
    {
        var marriage = await marriages.CreateAsync(
            ToPersonId(request.SpouseId!.Value),
            ToPersonId(request.OtherSpouseId!.Value),
            User.AccountId(),
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, CreatedMarriageResponse.From(marriage));
    }

    /// <summary>The non-deleted, inactive marriages with a spouse you maintain, those waiting for you first.</summary>
    [HttpGet("inactive")]
    public async Task<MarriageConsentListResponse> ListInactive(CancellationToken cancellationToken) =>
        MarriageConsentListResponse.From(await marriages.ListInactiveAsync(User.Viewer(), cancellationToken));

    /// <summary>
    /// Approves for the spouse you maintain, from pending or after a disagreement. 403
    /// <c>marriage.consent_not_maintainer</c>, 404 <c>marriage.not_found</c>, 409
    /// <c>marriage.consent_unchanged</c>, 422 <c>marriage.not_a_spouse</c>.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<MarriageConsentResponse> Approve(Guid id, MarriageConsentRequest request, CancellationToken cancellationToken) =>
        MarriageConsentResponse.From(await marriages.ApproveAsync(
            ToMarriageId(id), ToPersonId(request.SpouseId!.Value), User.Viewer(), cancellationToken));

    /// <summary>
    /// Declines for a pending spouse you maintain or withdraws your approval; the marriage is
    /// inactive at once. 403 <c>marriage.consent_not_maintainer</c>, 404 <c>marriage.not_found</c>,
    /// 409 <c>marriage.consent_unchanged</c>, 422 <c>marriage.sole_maintainer_must_delete</c>.
    /// </summary>
    [HttpPost("{id:guid}/disagree")]
    public async Task<MarriageConsentResponse> Disagree(Guid id, MarriageConsentRequest request, CancellationToken cancellationToken) =>
        MarriageConsentResponse.From(await marriages.DisagreeAsync(
            ToMarriageId(id), ToPersonId(request.SpouseId!.Value), User.Viewer(), cancellationToken));

    /// <summary>
    /// Marks the marriage former (ended) or current again; 204. Needs no consent. 403
    /// <c>marriage.not_spouse_maintainer</c>, 404 <c>marriage.not_found</c>, 409
    /// <c>marriage.former_unchanged</c> or <c>marriage.deleted</c>.
    /// </summary>
    [HttpPut("{id:guid}/former")]
    public async Task<NoContentResult> SetFormer(Guid id, MarriageFormerRequest request, CancellationToken cancellationToken)
    {
        await marriages.SetFormerAsync(ToMarriageId(id), request.IsFormer!.Value, User.AccountId(), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Deletes the marriage, in any consent state, when you maintain both spouses; 204. Anyone else
    /// withdraws their approval instead. 403 <c>marriage.not_sole_maintainer</c>, 404
    /// <c>marriage.not_found</c>, 409 <c>marriage.deleted</c>.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<NoContentResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await marriages.DeleteAsync(ToMarriageId(id), User.AccountId(), cancellationToken);
        return NoContent();
    }

    /// <summary>The person's spouses through active marriages, current ones first.</summary>
    [HttpGet("/api/people/{id:guid}/spouses")]
    public async Task<SpouseListResponse> ListSpouses(Guid id, CancellationToken cancellationToken) =>
        SpouseListResponse.From(await marriages.ListSpousesAsync(ToPersonId(id), User.Viewer(), cancellationToken));

    /// <summary>The empty GUID is never a person, so it is reported like any other unknown ID.</summary>
    private static PersonId ToPersonId(Guid id) =>
        id == Guid.Empty ? throw new PersonNotFoundException() : new PersonId(id);

    private static LinkId ToMarriageId(Guid id) =>
        id == Guid.Empty ? throw new MarriageNotFoundException() : new LinkId(id);
}
