using Microsoft.AspNetCore.Identity;

namespace FamilyGraph.Infrastructure.Identity;

/// <summary>
/// The ASP.NET Core Identity user behind an <c>AccountId</c> (D-008). It stays in Infrastructure;
/// the domain sees only the account ID. It stores the Google login, the email and whether Google
/// verified it, and no other profile data.
/// </summary>
internal sealed class UserAccount : IdentityUser<Guid>;
