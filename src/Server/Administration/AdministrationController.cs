using FamilyGraph.Application.Accounts;
using FamilyGraph.Application.People;
using FamilyGraph.Business.People;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Accounts;
using Server.People;

namespace Server.Administration;

/// <summary>
/// The administrator's Users page and profile transfers (D-035). A non-administrator is 403
/// <c>accounts.not_administrator</c> for the lists and <c>transfer.not_administrator</c> for the
/// transfers; an unknown account is 404 <c>account.not_found</c>.
/// </summary>
[ApiController]
[Authorize]
[Route("api/admin")]
public sealed class AdministrationController(AccountAdministrationService administration) : ControllerBase
{
    /// <summary>Every account, up to <c>Accounts:ListLimit</c> (1000), with how many profiles it maintains.</summary>
    [HttpGet("accounts")]
    public async Task<AccountListResponse> ListAccounts(CancellationToken cancellationToken) =>
        AccountListResponse.From(await administration.ListAccountsAsync(User.Viewer(), cancellationToken));

    /// <summary>The non-deleted profiles the account maintains, Private ones with their names.</summary>
    [HttpGet("accounts/{id:guid}/profiles")]
    public async Task<ProfileListResponse> ListProfiles(Guid id, CancellationToken cancellationToken) =>
        ProfileListResponse.From(await administration.ListProfilesAsync(User.Viewer(), ToAccountId(id), cancellationToken));

    /// <summary>
    /// Makes another account the maintainer of every profile this account maintains, without its
    /// approval. The same account is 409 <c>transfer.same_account</c>.
    /// </summary>
    [HttpPost("accounts/{id:guid}/transfer")]
    public async Task<TransferredProfilesResponse> TransferAll(
        Guid id, TransferRequest request, CancellationToken cancellationToken)
    {
        var count = await administration.TransferAllAsync(
            User.Viewer(), ToAccountId(id), ToAccountId(request.ToAccountId!.Value), cancellationToken);
        return new TransferredProfilesResponse(count);
    }

    /// <summary>
    /// Makes another account the profile's maintainer, without its approval; 204. Its current
    /// maintainer is 409 <c>transfer.same_maintainer</c>, a deleted profile 409 <c>person.deleted</c>.
    /// </summary>
    [HttpPost("people/{id:guid}/transfer")]
    public async Task<IActionResult> TransferProfile(Guid id, TransferRequest request, CancellationToken cancellationToken)
    {
        var personId = id == Guid.Empty ? throw new PersonNotFoundException() : new PersonId(id);
        await administration.TransferProfileAsync(
            User.Viewer(), personId, ToAccountId(request.ToAccountId!.Value), cancellationToken);
        return NoContent();
    }

    private static AccountId ToAccountId(Guid id) =>
        id == Guid.Empty ? throw new AccountNotFoundException() : new AccountId(id);
}
