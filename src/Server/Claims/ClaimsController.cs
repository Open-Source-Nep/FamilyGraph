using FamilyGraph.Application.Claims;
using FamilyGraph.Application.People;
using FamilyGraph.Business.Claims;
using FamilyGraph.Business.People;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Accounts;

namespace Server.Claims;

/// <summary>
/// Identity claims (PRD 04, D-051, D-052). A signed-in account claims a profile with an optional note; the
/// maintainer approves or rejects it while it is the profile's only open claim, the profile is
/// not claimed and the claimant has not asked for review (otherwise 403 <c>claim.administrator_only</c>);
/// administrators decide the rest with a rationale. A maintainer may mark a profile as
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

    /// <summary>The open claims on claimed profiles you maintain, for a reply (AC-04.9).</summary>
    [HttpGet("api/claims/to-respond")]
    public async Task<ClaimListResponse> ListToRespond(CancellationToken cancellationToken) =>
        ClaimListResponse.From(await claims.ListToRespondAsync(User.Viewer(), cancellationToken));

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

    /// <summary>
    /// Asks administrators to review your claim; 204. An appeal of the maintainer's rejection (once,
    /// 409 <c>claim.not_appealable</c>, or <c>claim.appeal_too_late</c> after its note was erased), or an
    /// open claim the maintainer has not answered in <c>Claims:MaintainerResponseDays</c> (409
    /// <c>claim.review_too_early</c>).
    /// </summary>
    [HttpPost("api/claims/{id:guid}/review")]
    public async Task<IActionResult> RequestReview(Guid id, CancellationToken cancellationToken)
    {
        await claims.RequestReviewAsync(ToClaimId(id), User.AccountId(), cancellationToken);
        return NoContent();
    }

    /// <summary>Changes the note of your open claim; 204. Claimant only.</summary>
    [HttpPut("api/claims/{id:guid}/evidence")]
    public async Task<IActionResult> ChangeEvidence(Guid id, ChangeClaimEvidenceRequest request, CancellationToken cancellationToken)
    {
        await claims.ChangeEvidenceAsync(ToClaimId(id), request.Evidence, User.AccountId(), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Replies, as the maintainer of a claimed profile, to an open claim on it; 204. Administrators
    /// read it and decide. An unclaimed profile is 409 <c>claim.not_claimed_profile</c>.
    /// </summary>
    [HttpPut("api/claims/{id:guid}/response")]
    public async Task<IActionResult> Respond(Guid id, ClaimResponseRequest request, CancellationToken cancellationToken)
    {
        await claims.RespondAsync(ToClaimId(id), request.Response, User.AccountId(), cancellationToken);
        return NoContent();
    }

    /// <summary>The open claims waiting for administrators, with every note. Administrators only (403 <c>claim.not_administrator</c>).</summary>
    [HttpGet("api/admin/claims")]
    public async Task<ClaimListResponse> ListForAdministrators(CancellationToken cancellationToken) =>
        ClaimListResponse.From(await claims.ListForAdministratorsAsync(User.Viewer(), cancellationToken));

    /// <summary>
    /// Approves a claim waiting for administrators, with a rationale (422 <c>claim.rationale_required</c>);
    /// the profile's other open claims are rejected; 204. A claim the maintainer still decides is 409
    /// <c>claim.for_maintainer</c>.
    /// </summary>
    [HttpPost("api/admin/claims/{id:guid}/approve")]
    public async Task<IActionResult> ApproveAsAdministrator(Guid id, ClaimDecisionRequest request, CancellationToken cancellationToken)
    {
        await claims.ApproveAsAdministratorAsync(ToClaimId(id), request.Rationale, User.Viewer(), cancellationToken);
        return NoContent();
    }

    /// <summary>Rejects a claim waiting for administrators, with a rationale; 204.</summary>
    [HttpPost("api/admin/claims/{id:guid}/reject")]
    public async Task<IActionResult> RejectAsAdministrator(Guid id, ClaimDecisionRequest request, CancellationToken cancellationToken)
    {
        await claims.RejectAsAdministratorAsync(ToClaimId(id), request.Rationale, User.Viewer(), cancellationToken);
        return NoContent();
    }

    /// <summary>Asks the claimant for more evidence; 204.</summary>
    [HttpPost("api/admin/claims/{id:guid}/ask")]
    public async Task<IActionResult> AskForEvidence(Guid id, ClaimQuestionRequest request, CancellationToken cancellationToken)
    {
        await claims.AskForEvidenceAsync(ToClaimId(id), request.Question, User.Viewer(), cancellationToken);
        return NoContent();
    }

    private static PersonId ToPersonId(Guid id) =>
        id == Guid.Empty ? throw new PersonNotFoundException() : new PersonId(id);

    private static ClaimId ToClaimId(Guid id) =>
        id == Guid.Empty ? throw new ClaimNotFoundException() : new ClaimId(id);
}
