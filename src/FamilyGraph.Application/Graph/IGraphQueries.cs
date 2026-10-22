using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.Graph;
using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Graph;

/// <summary>
/// Bounded reads of the active graph (D-002, D-009). Every query takes its limits, walks only
/// active links (PRD 03) and never loads the whole graph. A query that runs past
/// <see cref="GraphLimits.QueryTimeoutSeconds"/> throws <see cref="GraphQueryTimeoutException"/>.
/// </summary>
public interface IGraphQueries
{
    /// <summary>
    /// The centre and up to <paramref name="peopleLimit"/> people nearest to it in any direction,
    /// at most <paramref name="generations"/> generations above or below it and at most twice as
    /// many links away, with the active links between them (D-023).
    /// </summary>
    Task<LocalViewData> LocalViewAsync(
        PersonId centreId, AccountId viewerId, int generations, int peopleLimit, CancellationToken cancellationToken);

    /// <summary>
    /// The active links between the first <paramref name="peopleLimit"/> people reached from
    /// <paramref name="personId"/> in breadth-first order, so every shortest path to a reached
    /// person lies inside them (D-023).
    /// </summary>
    Task<LinkNeighbourhood> LinksAroundAsync(PersonId personId, int peopleLimit, CancellationToken cancellationToken);

    /// <summary>The given people, for privacy projection. Unknown IDs are left out.</summary>
    Task<IReadOnlyList<PersonRecord>> PeopleAsync(
        IReadOnlyCollection<PersonId> ids, AccountId viewerId, CancellationToken cancellationToken);
}

/// <summary>
/// A person in a local view: <see cref="Generation"/> is above (positive) or below (negative) the
/// centre, and <see cref="HasMore"/> says that they have active links to people not in the view.
/// </summary>
public sealed record LocalViewPersonData(PersonRecord Person, int Generation, bool HasMore);

/// <summary>The people of a local view, nearest first, and the active links between them.</summary>
public sealed record LocalViewData(
    IReadOnlyList<LocalViewPersonData> People, IReadOnlyList<GraphLink> Links, bool IsTruncated);

/// <summary><see cref="IsComplete"/> is false when the search stopped at its people limit.</summary>
public sealed record LinkNeighbourhood(IReadOnlyList<GraphLink> Links, bool IsComplete);
