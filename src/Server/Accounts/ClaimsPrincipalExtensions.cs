using System.Security.Claims;
using FamilyGraph.Application.Accounts;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.People;

namespace Server.Accounts;

internal static class ClaimsPrincipalExtensions
{
    /// <summary>The signed-in account. Only call it where <c>[Authorize]</c> guarantees a session.</summary>
    public static AccountId AccountId(this ClaimsPrincipal user) =>
        new(Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!));

    /// <summary>The signed-in account as a reader, with its administrator role from the session (D-008).</summary>
    public static Viewer Viewer(this ClaimsPrincipal user) =>
        new(user.AccountId(), user.IsInRole(AccountRoles.Administrator));
}
