namespace FamilyGraph.Application.People;

/// <summary>
/// Configurable list limits for people (D-010): <c>People:MaintainedListLimit</c> caps "profiles I
/// maintain" and <c>People:SearchLimit</c> the matches of a person search (D-032).
/// </summary>
public sealed record PeopleLimits(
    int MaintainedListLimit = PeopleLimits.DefaultMaintainedListLimit,
    int SearchLimit = PeopleLimits.DefaultSearchLimit)
{
    public const int DefaultMaintainedListLimit = 500;

    public const int DefaultSearchLimit = 20;
}
