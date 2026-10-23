using FamilyGraph.Application.Graph;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.Graph;

namespace Server.Graph;

/// <summary>
/// A person as you may see them in the graph. <see cref="MaintainedByYou"/> never names the
/// maintaining account (D-019). A person hidden from you has <see cref="Placeholder"/>
/// <c>Private</c> or <c>Deleted</c> and no name, and stays in the graph with their links (PRD 05).
/// </summary>
public sealed record GraphPersonResponse(
    Guid Id,
    string? FirstName,
    string? MiddleName,
    string? LastName,
    string? Placeholder,
    string Visibility,
    bool MaintainedByYou)
{
    internal static GraphPersonResponse From(PersonReference person) => new(
        person.Id.Value,
        person.Name?.FirstName,
        person.Name?.MiddleName,
        person.Name?.LastName,
        person.Placeholder?.Name,
        person.Visibility.Name,
        person.MaintainedByViewer);
}

/// <summary>
/// A person in a local view. <see cref="Generation"/> is above (positive) or below (negative) the
/// centre; <see cref="HasMore"/> means they have relatives not in the view, which a view centred on
/// them loads (D-010).
/// </summary>
public sealed record LocalViewPersonResponse(GraphPersonResponse Person, int Generation, bool HasMore);

/// <summary>An active parent–child link, <c>Biological</c> or <c>Adoptive</c>.</summary>
public sealed record GraphLinkResponse(Guid Id, Guid ParentId, Guid ChildId, string Type)
{
    internal static GraphLinkResponse From(GraphLink link) =>
        new(link.Id.Value, link.ParentId.Value, link.ChildId.Value, link.Type.Name);
}

/// <summary>
/// The centre and the people nearest to it, nearest first, with the active links between them.
/// <see cref="IsTruncated"/> says more people were in reach (D-010, D-023).
/// </summary>
public sealed record LocalViewResponse(
    Guid CentreId, IReadOnlyList<LocalViewPersonResponse> People, IReadOnlyList<GraphLinkResponse> Links, bool IsTruncated)
{
    internal static LocalViewResponse From(LocalView view) => new(
        view.CentreId.Value,
        [.. view.People.Select(row => new LocalViewPersonResponse(GraphPersonResponse.From(row.Person), row.Generation, row.HasMore))],
        [.. view.Links.Select(GraphLinkResponse.From)],
        view.IsTruncated);
}

/// <summary>
/// Everyone connected through active links to your profiles, nearest first, and the active links
/// between them. Nobody is the centre. <see cref="IsTruncated"/> says more people were in reach (D-024).
/// </summary>
public sealed record GraphOverviewResponse(
    IReadOnlyList<GraphPersonResponse> People, IReadOnlyList<GraphLinkResponse> Links, bool IsTruncated)
{
    internal static GraphOverviewResponse From(Overview overview) => new(
        [.. overview.People.Select(GraphPersonResponse.From)],
        [.. overview.Links.Select(GraphLinkResponse.From)],
        overview.IsTruncated);
}

/// <summary>
/// One step of a path: <see cref="Relation"/> is <c>Parent</c> or <c>Child</c>, how
/// <see cref="Person"/> relates to the previous person, and <see cref="Type"/> the link's type (PRD 08).
/// </summary>
public sealed record ConnectionStepResponse(Guid LinkId, string Relation, string Type, GraphPersonResponse Person);

public sealed record ConnectionResponse(GraphPersonResponse Start, IReadOnlyList<ConnectionStepResponse> Steps);

/// <summary>
/// The shortest path first, then further paths. <see cref="MorePathsExist"/> says that paths beyond
/// those listed were found. When <see cref="SearchComplete"/> is false, no path means no connection
/// was found within the search limit, not that the people are unrelated (D-009).
/// </summary>
public sealed record ConnectionsResponse(IReadOnlyList<ConnectionResponse> Paths, bool MorePathsExist, bool SearchComplete)
{
    internal static ConnectionsResponse From(Connections connections) => new(
        [.. connections.Paths.Select(path => new ConnectionResponse(
            GraphPersonResponse.From(path.Start),
            [.. path.Steps.Select(step => new ConnectionStepResponse(
                step.Link.Id.Value, step.Relation.Name, step.Link.Type.Name, GraphPersonResponse.From(step.Person)))]))],
        connections.MorePathsExist,
        connections.SearchComplete);
}
