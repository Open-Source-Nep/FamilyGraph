namespace FamilyGraph.Application.Claims;

/// <summary>How many claims a list returns (<c>Claims:ListLimit</c>, D-010).</summary>
public sealed record ClaimLimits(int ListLimit = ClaimLimits.DefaultListLimit)
{
    public const int DefaultListLimit = 100;
}
