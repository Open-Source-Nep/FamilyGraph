using System.Security.Claims;
using FamilyGraph.Business.People;

namespace Server.Accounts;

internal static class ClaimsPrincipalExtensions
{
    /// <summary>The signed-in account. Only call it where <c>[Authorize]</c> guarantees a session.</summary>
    public static AccountId AccountId(this ClaimsPrincipal user) =>
        new(Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!));
}
