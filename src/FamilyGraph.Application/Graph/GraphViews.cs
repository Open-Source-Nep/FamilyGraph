using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.Graph;
using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Graph;

/// <summary>A person in a local view, as the viewer may see them (D-010).</summary>
public sealed record LocalViewPerson(PersonReference Person, int Generation, bool HasMore);

/// <summary>
/// A local view around <see cref="CentreId"/> (PRD 08, D-010): the people nearest first and the
/// active links and marriages between them. <see cref="IsTruncated"/> says that more people were in reach.
/// </summary>
public sealed record LocalView(
    PersonId CentreId,
    IReadOnlyList<LocalViewPerson> People,
    IReadOnlyList<GraphLink> Links,
    IReadOnlyList<GraphMarriage> Marriages,
    bool IsTruncated);

/// <summary>
/// Everyone connected through active links to the profiles the viewer maintains, nearest first, up
/// to the configured limit, and the active links and marriages between them (D-024, D-031). Nobody
/// is the centre. <see cref="IsTruncated"/> says more people were in reach.
/// </summary>
public sealed record Overview(
    IReadOnlyList<PersonReference> People,
    IReadOnlyList<GraphLink> Links,
    IReadOnlyList<GraphMarriage> Marriages,
    bool IsTruncated);

/// <summary>
/// A step of a path: the link or marriage, how the person reached relates to the previous one, and
/// that person.
/// </summary>
public sealed record ConnectionStep(GraphEdge Link, StepRelation Relation, PersonReference Person);

public sealed record Connection(PersonReference Start, IReadOnlyList<ConnectionStep> Steps);

/// <summary>
/// How two people are connected (PRD 08): the shortest path first, then up to the configured number
/// of further paths. <see cref="MorePathsExist"/> says that paths beyond those were found.
/// <see cref="SearchComplete"/> is false when the search stopped at its people limit, so no
/// connection means "none found within the search limit", not "not related" (D-009).
/// </summary>
public sealed record Connections(IReadOnlyList<Connection> Paths, bool MorePathsExist, bool SearchComplete);
