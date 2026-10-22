namespace Server.Configuration;

/// <summary>Instance settings for the Client, from <c>GET /api/configuration</c>.</summary>
public sealed record ClientConfigurationResponse(GoogleSignInConfiguration Google);

/// <summary>
/// Whether this instance can sign users in with Google (<c>Authentication:Google</c>), and the OAuth
/// client ID the Client sends to Google. The client ID is public; there is no client secret.
/// </summary>
public sealed record GoogleSignInConfiguration(bool Enabled, string? ClientId);
