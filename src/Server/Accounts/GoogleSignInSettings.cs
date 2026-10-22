namespace Server.Accounts;

/// <summary>
/// The <c>Authentication:Google</c> configuration the Server reads once at startup. The client ID is
/// public: the Client sends it to Google, and the Server checks it is the audience of the ID token.
/// </summary>
public sealed record GoogleSignInSettings(string? ClientId)
{
    public const string Section = "Authentication:Google";

    public bool Enabled => !string.IsNullOrEmpty(ClientId);

    public static GoogleSignInSettings From(IConfiguration configuration) =>
        new(configuration.GetSection(Section)["ClientId"]);
}
