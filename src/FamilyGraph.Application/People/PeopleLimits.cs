namespace FamilyGraph.Application.People;

/// <summary>Configurable list limits for people (<c>People:MaintainedListLimit</c>, D-010).</summary>
public sealed record PeopleLimits(int MaintainedListLimit = PeopleLimits.DefaultMaintainedListLimit)
{
    public const int DefaultMaintainedListLimit = 500;
}
