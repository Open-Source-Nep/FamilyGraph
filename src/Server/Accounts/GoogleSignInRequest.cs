using System.ComponentModel.DataAnnotations;

namespace Server.Accounts;

/// <summary>
/// The ID token Google returned to the Client's <c>/google-callback</c> page, and the secret whose
/// SHA-256 the Client sent to Google as the nonce. Only the browser that started sign-in knows the
/// secret, so a leaked token cannot be replayed from elsewhere.
/// </summary>
public sealed class GoogleSignInRequest
{
    [Required]
    [StringLength(8192)]
    public string IdToken { get; init; } = "";

    [Required]
    [StringLength(128)]
    public string NonceSecret { get; init; } = "";
}
