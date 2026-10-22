namespace Server.Accounts;

/// <summary>
/// The <c>Authentication:Google</c> configuration the Server reads once at startup. Only whether it
/// is configured reaches the Client; the client ID and secret stay on the Server.
/// </summary>
public sealed record GoogleSignInSettings(string? ClientId, string? ClientSecret)
{
    public const string Section = "Authentication:Google";

    public bool Enabled => !string.IsNullOrEmpty(ClientId);

    public static GoogleSignInSettings From(IConfiguration configuration)
    {
        var section = configuration.GetSection(Section);
        return new(section["ClientId"], section["ClientSecret"]);
    }
}
