using Google.Apis.Auth;

namespace Server.Accounts;

/// <summary>What a valid Google ID token says about its user (D-018).</summary>
public sealed record GoogleIdToken(string Subject, string Email, bool EmailVerified, string? Nonce);

/// <summary>Checks an ID token the Client received from Google.</summary>
public interface IGoogleIdTokenValidator
{
    /// <summary>
    /// The token's claims, or <c>null</c> when it is not a current Google ID token for this
    /// instance's client ID or has no email.
    /// </summary>
    Task<GoogleIdToken?> ValidateAsync(string idToken);
}

/// <summary>
/// Validates the signature against Google's published keys (cached and refreshed by
/// <c>Google.Apis.Auth</c>), the issuer, the expiry and the audience.
/// </summary>
internal sealed class GoogleIdTokenValidator(GoogleSignInSettings google) : IGoogleIdTokenValidator
{
    public async Task<GoogleIdToken?> ValidateAsync(string idToken)
    {
        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken, new GoogleJsonWebSignature.ValidationSettings { Audience = [google.ClientId!] });
        }
        catch (InvalidJwtException)
        {
            return null;
        }

        return payload.Email is null ? null : new(payload.Subject, payload.Email, payload.EmailVerified, payload.Nonce);
    }
}
