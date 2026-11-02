using Server.Localization;

namespace Server.Configuration;

/// <summary>Instance settings for the Client, from <c>GET /api/configuration</c>.</summary>
public sealed record ClientConfigurationResponse(
    GoogleSignInConfiguration Google, LanguageConfiguration Languages, CultureConfiguration Cultures);

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

/// <summary>
/// The culture whose relationship labels the Client starts with (<c>Cultures:Default</c>) and every
/// culture it can switch to (D-048).
/// </summary>
public sealed record CultureConfiguration(string Default, IReadOnlyList<Culture> Available);

/// <summary>
/// A culture for relationship labels: its name in each of its <see cref="Languages"/>, the first of
/// which applies when the current language is not one of them (D-048).
/// </summary>
public sealed record Culture(string Code, IReadOnlyDictionary<string, string> Name, IReadOnlyList<string> Languages);
