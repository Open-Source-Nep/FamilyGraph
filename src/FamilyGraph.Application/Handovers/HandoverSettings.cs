namespace FamilyGraph.Application.Handovers;

/// <summary>How long a handover link stays valid (<c>Handovers:LinkLifetimeDays</c>, PRD 01).</summary>
public sealed record HandoverSettings(int LinkLifetimeDays = HandoverSettings.DefaultLinkLifetimeDays)
{
    public const int DefaultLinkLifetimeDays = 14;

    public const int MaxLinkLifetimeDays = 90;
}
