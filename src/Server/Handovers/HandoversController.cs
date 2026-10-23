using FamilyGraph.Application.Handovers;
using FamilyGraph.Application.People;
using FamilyGraph.Business.People;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Accounts;

namespace Server.Handovers;

/// <summary>
/// Voluntary profile handover (PRD 01): the maintainer creates a one-time link for a profile and
/// shares it; a signed-in account that opens it sees the profile and may accept, becoming its
/// maintainer. Unknown links are 404 <c>handover.not_found</c>; used, cancelled or void ones 409
/// <c>handover.closed</c>, expired ones 409 <c>handover.expired</c>.
/// </summary>
[ApiController]
[Authorize]
public sealed class HandoversController(HandoverService handovers) : ControllerBase
{
    /// <summary>
    /// Creates a handover link and replaces the profile's open one; 201 with the secret, which is
    /// shown only now. Only the maintainer may (403 <c>person.not_maintainer</c>).
    /// </summary>
    [HttpPost("api/people/{id:guid}/handover")]
    public async Task<ActionResult<CreatedHandoverResponse>> Offer(Guid id, CancellationToken cancellationToken)
    {
        var handover = await handovers.OfferAsync(ToPersonId(id), User.AccountId(), cancellationToken);
        return CreatedAtAction(nameof(GetOpen), new { id }, CreatedHandoverResponse.From(handover));
    }

    /// <summary>The profile's open handover link, or 204 when there is none. Maintainer only.</summary>
    [HttpGet("api/people/{id:guid}/handover")]
    public async Task<ActionResult<OpenHandoverResponse>> GetOpen(Guid id, CancellationToken cancellationToken)
    {
        var handover = await handovers.GetOpenAsync(ToPersonId(id), User.AccountId(), cancellationToken);
        return handover is null ? NoContent() : OpenHandoverResponse.From(handover);
    }

    /// <summary>Cancels the profile's open handover link; 204. Maintainer only.</summary>
    [HttpDelete("api/people/{id:guid}/handover")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        await handovers.CancelAsync(ToPersonId(id), User.AccountId(), cancellationToken);
        return NoContent();
    }

    /// <summary>The profile a link hands over. Your own profile is 409 <c>handover.own_profile</c>.</summary>
    [HttpPost("api/handovers/preview")]
    public async Task<HandoverPreviewResponse> Preview(HandoverTokenRequest request, CancellationToken cancellationToken) =>
        HandoverPreviewResponse.From(await handovers.PreviewAsync(request.Token!, User.Viewer(), cancellationToken));

    /// <summary>Accepts the link: you become the profile's maintainer. Its links and their consent are unchanged.</summary>
    [HttpPost("api/handovers/accept")]
    public async Task<AcceptedHandoverResponse> Accept(HandoverTokenRequest request, CancellationToken cancellationToken)
    {
        var accepted = await handovers.AcceptAsync(request.Token!, User.AccountId(), cancellationToken);
        return new AcceptedHandoverResponse(accepted.PersonId.Value);
    }

    private static PersonId ToPersonId(Guid id) =>
        id == Guid.Empty ? throw new PersonNotFoundException() : new PersonId(id);
}
