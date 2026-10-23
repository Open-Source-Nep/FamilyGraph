using System.ComponentModel.DataAnnotations;

namespace Server.Handovers;

/// <summary>The secret from a handover link. It is sent in the body so it stays out of URLs and logs.</summary>
public sealed class HandoverTokenRequest
{
    [Required]
    [MaxLength(100)]
    public string? Token { get; init; }
}
