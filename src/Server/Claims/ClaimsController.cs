using FamilyGraph.Application.Claims;
using FamilyGraph.Application.People;
using FamilyGraph.Business.Claims;
using FamilyGraph.Business.People;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Accounts;

namespace Server.Claims;

/// <summary>
/// Identity claims (PRD 04, D-051). A signed-in account claims a profile with an optional note; the
/// maintainer approves or rejects it while it is the profile's only open claim and the profile is
/// not claimed (otherwise 403 <c>claim.administrator_only</c>). A maintainer may mark a profile as
/// themselves. An account with a claimed profile or an open claim cannot claim again (409
/// <c>claim.account_has_claim</c>). Relationship consent never changes here.
/// </summary>
[ApiController]
[Authorize]
public sealed class ClaimsController(ClaimService claims) : ControllerBase
{
    /// <summary>The profile's claim state for you, for the person page.</summary>
    [HttpGet("api/people/{id:guid}/claims")]
    public async Task<ProfileClaimStatusResponse> GetForPerson(Guid id, CancellationToken cancellationToken) =>
        ProfileClaimStatusResponse.From(await claims.GetForPersonAsync(ToPersonId(id), User.Viewer(), cancellationToken));

    /// <summary>Claims the profile; 201 with the open claim.</summary>
    [HttpPost("api/people/{id:guid}/claims")]
    public async Task<ActionResult<ClaimResponse>> Submit(Guid id, SubmitClaimRequest request, CancellationToken cancellationToken)
    {
        var claim = await claims.SubmitAsync(ToPersonId(id), request.Evidence, User.Viewer(), cancellationToken);
        return CreatedAtAction(nameof(GetForPerson), new { id }, ClaimResponse.From(claim));
    }

    /// <summary>"This is me": marks a profile you maintain as yourself; 204.</summary>
    [HttpPost("api/people/{id:guid}/claim-as-self")]
    public async Task<IActionResult> ClaimAsSelf(Guid id, CancellationToken cancellationToken)
    {
        await claims.ClaimAsSelfAsync(ToPersonId(id), User.AccountId(), cancellationToken);
        return NoContent();
    }

    /// <summary>The claims waiting for your decision as maintainer.</summary>
    [HttpGet("api/claims/to-decide")]
    public async Task<ClaimListResponse> ListToDecide(CancellationToken cancellationToken) =>
        ClaimListResponse.From(await claims.ListToDecideAsync(User.Viewer(), cancellationToken));

    /// <summary>Your own claims, newest first.</summary>
    [HttpGet("api/claims/mine")]
    public async Task<ClaimListResponse> ListMine(CancellationToken cancellationToken) =>
        ClaimListResponse.From(await claims.ListMineAsync(User.Viewer(), cancellationToken));

    /// <summary>Approves: the claimant becomes the maintainer and the claimed person; 204. Maintainer only.</summary>
    [HttpPost("api/claims/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        await claims.ApproveAsync(ToClaimId(id), User.AccountId(), cancellationToken);
        return NoContent();
    }

    /// <summary>Rejects the claim; 204. Maintainer only.</summary>
    [HttpPost("api/claims/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, CancellationToken cancellationToken)
    {
        await claims.RejectAsync(ToClaimId(id), User.AccountId(), cancellationToken);
        return NoContent();
    }

    /// <summary>Withdraws your open claim; 204. Claimant only (403 <c>claim.not_claimant</c>).</summary>
    [HttpPost("api/claims/{id:guid}/withdraw")]
    public async Task<IActionResult> Withdraw(Guid id, CancellationToken cancellationToken)
    {
        await claims.WithdrawAsync(ToClaimId(id), User.AccountId(), cancellationToken);
        return NoContent();
    }

    private static PersonId ToPersonId(Guid id) =>
        id == Guid.Empty ? throw new PersonNotFoundException() : new PersonId(id);

    private static ClaimId ToClaimId(Guid id) =>
        id == Guid.Empty ? throw new ClaimNotFoundException() : new ClaimId(id);
}
