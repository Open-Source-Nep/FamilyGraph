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

/// <summary>
/// A person in a Bansawali, as the viewer may see them: <see cref="Generation"/> below the
/// ancestor, whether the line <see cref="Continues"/> through them, and <see cref="HasMore"/> when
/// the line continues but their children were cut off by the limit (D-043).
/// </summary>
public sealed record BansawaliPerson(PersonReference Person, int Generation, bool Continues, bool HasMore);

/// <summary>
/// A Bansawali from <see cref="RootId"/> (PRD 08, D-043): the ancestor first, then each generation.
/// <see cref="Links"/> are the links that place each descendant under one parent, and
/// <see cref="Spouses"/> the people married to them through <see cref="Marriages"/>, who never
/// continue the line. <see cref="IsTruncated"/> says the people or marriages limit was reached.
/// </summary>
public sealed record Bansawali(
    PersonId RootId,
    BansawaliLine Line,
    IReadOnlyList<BansawaliPerson> People,
    IReadOnlyList<GraphLink> Links,
    IReadOnlyList<PersonReference> Spouses,
    IReadOnlyList<GraphMarriage> Marriages,
    bool IsTruncated);
