namespace Server.Configuration;

/// <summary>Instance settings for the Client, from <c>GET /api/configuration</c>.</summary>
public sealed record ClientConfigurationResponse(GoogleSignInConfiguration Google);

/// <summary>Whether this instance can sign users in with Google (<c>Authentication:Google</c>).</summary>
public sealed record GoogleSignInConfiguration(bool Enabled);
