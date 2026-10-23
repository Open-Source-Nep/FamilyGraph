using System.Security.Cryptography;
using System.Text;

namespace FamilyGraph.Application.Handovers;

/// <summary>
/// The secret in a handover link: 32 random bytes, URL-safe Base64. Only its SHA-256 hash is
/// stored, so the database never holds a usable link.
/// </summary>
public static class HandoverToken
{
    private const int MaxLength = 100;

    public static string New() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    /// <summary>The hash a link is stored and looked up by, or <see langword="null"/> for a value that cannot be a token.</summary>
    public static string? HashOf(string? token) =>
        string.IsNullOrWhiteSpace(token) || token.Length > MaxLength
            ? null
            : Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
