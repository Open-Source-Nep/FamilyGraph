namespace FamilyGraph.Application.People;

/// <summary>
/// Configurable list limits for people (D-010): <c>People:MaintainedListLimit</c> caps "profiles I
/// maintain", <c>People:SearchLimit</c> the matches of a person search (D-032) and
/// <c>People:PageSize</c> one page of the All people list (D-042).
/// </summary>
public sealed record PeopleLimits(
    int MaintainedListLimit = PeopleLimits.DefaultMaintainedListLimit,
    int SearchLimit = PeopleLimits.DefaultSearchLimit,
    int PageSize = PeopleLimits.DefaultPageSize)
{
    public const int DefaultMaintainedListLimit = 500;

    public const int DefaultSearchLimit = 20;

    public const int DefaultPageSize = 50;
}
