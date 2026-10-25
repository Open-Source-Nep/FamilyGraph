using FamilyGraph.Application.Graph;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.Graph;

namespace Server.Graph;

/// <summary>
/// A person as you may see them in the graph. <see cref="MaintainedByYou"/> never names the
/// maintaining account (D-019). A person hidden from you has <see cref="Placeholder"/>
/// <c>Private</c> or <c>Deleted</c> and no name or image, and stays in the graph with their links
/// (PRD 05). <see cref="ImageId"/> names the current image, if any (D-028). <see cref="Gender"/> is
/// null when not set or for a placeholder (D-030), and so is <see cref="Description"/> (D-037, D-038).
/// <see cref="BirthSortDate"/> is the earliest day the birth date can mean, only when its
/// maintainer shows it to others, so children sort the same way for every viewer (D-039).
/// </summary>
public sealed record GraphPersonResponse(
    Guid Id,
    string? FirstName,
    string? MiddleName,
    string? LastName,
    string? Placeholder,
    string Visibility,
    bool MaintainedByYou,
    Guid? ImageId,
    string? Gender,
    string? Description,
    DateOnly? BirthSortDate)
{
    internal static GraphPersonResponse From(PersonReference person) => new(
        person.Id.Value,
        person.Name?.FirstName,
        person.Name?.MiddleName,
        person.Name?.LastName,
        person.Placeholder?.Name,
        person.Visibility.Name,
        person.MaintainedByViewer,
        person.ImageId?.Value,
        person.Gender?.Name,
        person.Description,
        person.BirthDateShown ? person.BirthDate?.SortDate : null);
}

/// <summary>
/// A person in a local view. <see cref="Generation"/> is above (positive) or below (negative) the
/// centre; <see cref="HasMore"/> means they have relatives not in the view, which a view centred on
/// them loads (D-010).
/// </summary>
public sealed record LocalViewPersonResponse(GraphPersonResponse Person, int Generation, bool HasMore);

/// <summary>
/// An active parent–child link, <c>Biological</c> or <c>Adoptive</c>, with the child's place in
/// the parent's birth order when set (D-016, D-039).
/// </summary>
public sealed record GraphLinkResponse(Guid Id, Guid ParentId, Guid ChildId, string Type, int? BirthOrder)
{
    internal static GraphLinkResponse From(GraphLink link) =>
        new(link.Id.Value, link.ParentId.Value, link.ChildId.Value, link.Type.Name, link.BirthOrder);
}

/// <summary>An active marriage between two people in the same generation; <see cref="IsFormer"/> when it ended (D-031).</summary>
public sealed record GraphMarriageResponse(Guid Id, Guid FirstSpouseId, Guid SecondSpouseId, bool IsFormer)
{
    internal static GraphMarriageResponse From(GraphMarriage marriage) =>
        new(marriage.Id.Value, marriage.FirstSpouseId.Value, marriage.SecondSpouseId.Value, marriage.IsFormer);
}

/// <summary>
/// The centre and the people nearest to it, nearest first, with the active links and marriages
/// between them. <see cref="IsTruncated"/> says more people were in reach (D-010, D-023, D-031).
/// </summary>
public sealed record LocalViewResponse(
    Guid CentreId,
    IReadOnlyList<LocalViewPersonResponse> People,
    IReadOnlyList<GraphLinkResponse> Links,
    IReadOnlyList<GraphMarriageResponse> Marriages,
    bool IsTruncated)
{
    internal static LocalViewResponse From(LocalView view) => new(
        view.CentreId.Value,
        [.. view.People.Select(row => new LocalViewPersonResponse(GraphPersonResponse.From(row.Person), row.Generation, row.HasMore))],
        [.. view.Links.Select(GraphLinkResponse.From)],
        [.. view.Marriages.Select(GraphMarriageResponse.From)],
        view.IsTruncated);
}

/// <summary>
/// Everyone connected through active links and marriages to your profiles, nearest first, and the
/// active links and marriages between them. Nobody is the centre. <see cref="IsTruncated"/> says
/// more people were in reach (D-024, D-031).
/// </summary>
public sealed record GraphOverviewResponse(
    IReadOnlyList<GraphPersonResponse> People,
    IReadOnlyList<GraphLinkResponse> Links,
    IReadOnlyList<GraphMarriageResponse> Marriages,
    bool IsTruncated)
{
    internal static GraphOverviewResponse From(Overview overview) => new(
        [.. overview.People.Select(GraphPersonResponse.From)],
        [.. overview.Links.Select(GraphLinkResponse.From)],
        [.. overview.Marriages.Select(GraphMarriageResponse.From)],
        overview.IsTruncated);
}

/// <summary>
/// One step of a path: <see cref="Relation"/> is <c>Parent</c>, <c>Child</c> or <c>Spouse</c>, how
/// <see cref="Person"/> relates to the previous person, and <see cref="Type"/> the link's type:
/// <c>Biological</c> or <c>Adoptive</c> for a parent or child, <c>Marriage</c> or
/// <c>FormerMarriage</c> for a spouse (PRD 08, D-031).
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
                step.Link.Id.Value, step.Relation.Name, TypeOf(step.Link), GraphPersonResponse.From(step.Person)))]))],
        connections.MorePathsExist,
        connections.SearchComplete);

    private static string TypeOf(GraphEdge edge) => edge switch
    {
        GraphLink link => link.Type.Name,
        GraphMarriage { IsFormer: true } => "FormerMarriage",
        GraphMarriage => "Marriage",
        _ => throw new ArgumentException("Unknown kind of graph edge.", nameof(edge)),
    };
}
