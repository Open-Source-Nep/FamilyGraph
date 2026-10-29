using Server.Localization;

namespace Server.Configuration;

/// <summary>Instance settings for the Client, from <c>GET /api/configuration</c>.</summary>
public sealed record ClientConfigurationResponse(GoogleSignInConfiguration Google, LanguageConfiguration Languages);

/// <summary>
/// Whether this instance can sign users in with Google (<c>Authentication:Google</c>), and the OAuth
/// client ID the Client sends to Google. The client ID is public; there is no client secret.
/// </summary>
public sealed record GoogleSignInConfiguration(bool Enabled, string? ClientId);

/// <summary>
/// The language the Client starts in (<c>Languages:Default</c>) and every language it can switch to,
/// English first (D-047).
/// </summary>
public sealed record LanguageConfiguration(string Default, IReadOnlyList<Language> Available);
