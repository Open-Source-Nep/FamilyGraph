namespace FamilyGraph.Application.Accounts;

/// <summary>
/// What an external sign-in provider tells us about a user (D-008). <paramref name="ProviderKey"/>
/// is the provider's stable user ID; the email may change between sign-ins.
/// </summary>
public sealed record ExternalLogin(string Provider, string ProviderKey, string Email, bool EmailVerified)
{
    /// <summary>The provider name of a Google login, the only sign-in (D-008).</summary>
    public const string GoogleProvider = "Google";
}
