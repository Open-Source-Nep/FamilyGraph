using FamilyGraph.Application.Graph;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.Graph;
using FamilyGraph.Business.People;
using Server.IntegrationTests.People;

namespace Server.IntegrationTests.Graph;

/// <summary>
/// Graph queries over the in-memory people and links: a breadth-first walk of the active links and marriages with
/// the same order and bounds as the database query (D-023). It can also time out like one.
/// </summary>
public sealed class InMemoryGraph(InMemoryPeople store) : IGraphQueries
{
    /// <summary>Makes every query throw as if it ran past the graph timeout.</summary>
    public bool TimesOut { get; set; }

    public Task<LocalViewData> LocalViewAsync(
        PersonId centreId, AccountId viewerId, int generations, int peopleLimit, CancellationToken cancellationToken)
    {
        ThrowIfTimingOut();
        var nearest = new Dictionary<PersonId, (int Distance, int Generation)> { [centreId] = (0, 0) };
        var frontier = new List<PersonId> { centreId };
        for (var distance = 1; distance <= 2 * generations && frontier.Count > 0; distance++)
        {
            var next = new List<PersonId>();
            foreach (var person in frontier)
            {
                foreach (var (other, offset) in Neighbours(person))
                {
                    var generation = nearest[person].Generation + offset;
                    if (Math.Abs(generation) <= generations && nearest.TryAdd(other, (distance, generation)))
                    {
                        next.Add(other);
                    }
                }
            }

            frontier = next;
        }

        var ordered = nearest
            .OrderBy(entry => entry.Value.Distance)
            .ThenBy(entry => Math.Abs(entry.Value.Generation))
            .ThenByDescending(entry => entry.Value.Generation)
            .ThenBy(entry => entry.Key.Value)
            .ToList();
        var shown = ordered.Take(peopleLimit + 1).ToList();
        var ids = shown.Select(entry => entry.Key).ToHashSet();
        return Task.FromResult(new LocalViewData(
            [.. shown.Select(entry => new LocalViewPersonData(
                Record(entry.Key, viewerId),
                entry.Value.Generation,
                Neighbours(entry.Key).Any(neighbour => !ids.Contains(neighbour.Person))))],
            [.. ActiveLinks().Where(link => ids.Contains(link.ParentId) && ids.Contains(link.ChildId))],
            [.. ActiveMarriages().Where(marriage => ids.Contains(marriage.FirstSpouseId) && ids.Contains(marriage.SecondSpouseId))],
            ordered.Count > shown.Count));
    }

    public Task<LinkNeighbourhood> LinksAroundAsync(PersonId personId, int peopleLimit, CancellationToken cancellationToken)
    {
        ThrowIfTimingOut();
        return Task.FromResult(new LinkNeighbourhood([.. ActiveLinks()], [.. ActiveMarriages()], IsComplete: true));
    }

    public Task<OverviewData> OverviewAsync(
        IReadOnlyCollection<PersonId> startIds, AccountId viewerId, int peopleLimit, CancellationToken cancellationToken)
    {
        ThrowIfTimingOut();
        var reached = startIds.Distinct().ToList();
        var seen = reached.ToHashSet();
        for (var index = 0; index < reached.Count && reached.Count <= peopleLimit; index++)
        {
            foreach (var (other, _) in Neighbours(reached[index]))
            {
                if (seen.Add(other))
                {
                    reached.Add(other);
                }
            }
        }

        var shown = reached.Take(peopleLimit).ToList();
        var ids = shown.ToHashSet();
        return Task.FromResult(new OverviewData(
            [.. shown.Select(id => Record(id, viewerId))],
            [.. ActiveLinks().Where(link => ids.Contains(link.ParentId) && ids.Contains(link.ChildId))],
            [.. ActiveMarriages().Where(marriage => ids.Contains(marriage.FirstSpouseId) && ids.Contains(marriage.SecondSpouseId))],
            reached.Count > shown.Count));
    }

    public Task<ChildLinks> ChildLinksAsync(
        IReadOnlyCollection<PersonId> parentIds, int limit, CancellationToken cancellationToken)
    {
        ThrowIfTimingOut();
        var links = ActiveLinks()
            .Where(link => parentIds.Contains(link.ParentId))
            .OrderBy(link => link.ParentId.Value)
            .ThenBy(link => link.BirthOrder ?? int.MaxValue)
            .ThenBy(link => link.Id.Value)
            .ToList();
        return Task.FromResult(new ChildLinks([.. links.Take(limit)], links.Count > limit));
    }

    public Task<IReadOnlySet<PersonId>> WithChildrenAsync(
        IReadOnlyCollection<PersonId> parentIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<PersonId>>(
            ActiveLinks().Select(link => link.ParentId).Where(parentIds.Contains).ToHashSet());

    public Task<MarriagesOf> MarriagesOfAsync(
        IReadOnlyCollection<PersonId> personIds, int limit, CancellationToken cancellationToken)
    {
        ThrowIfTimingOut();
        var marriages = ActiveMarriages()
            .Where(marriage => personIds.Contains(marriage.FirstSpouseId) || personIds.Contains(marriage.SecondSpouseId))
            .OrderBy(marriage => marriage.Id.Value)
            .ToList();
        return Task.FromResult(new MarriagesOf([.. marriages.Take(limit)], marriages.Count > limit));
    }

    public Task<IReadOnlyList<PersonRecord>> PeopleAsync(
        IReadOnlyCollection<PersonId> ids, AccountId viewerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PersonRecord>>(
            [.. ids.Where(store.People.ContainsKey).Select(id => Record(id, viewerId))]);

    private void ThrowIfTimingOut()
    {
        if (TimesOut)
        {
            throw new GraphQueryTimeoutException(new OperationCanceledException());
        }
    }

    private PersonRecord Record(PersonId id, AccountId viewerId) =>
        PersonRecord.From(store.People[id], viewerId);

    private IEnumerable<(PersonId Person, int Offset)> Neighbours(PersonId person) =>
        ActiveLinks()
            .Where(link => link.ChildId == person).Select(link => (link.ParentId, 1))
            .Concat(ActiveLinks().Where(link => link.ParentId == person).Select(link => (link.ChildId, -1)))
            .Concat(ActiveMarriages().Where(marriage => marriage.FirstSpouseId == person).Select(marriage => (marriage.SecondSpouseId, 0)))
            .Concat(ActiveMarriages().Where(marriage => marriage.SecondSpouseId == person).Select(marriage => (marriage.FirstSpouseId, 0)));

    private IEnumerable<GraphLink> ActiveLinks() =>
        store.Links.Values
            .Where(link => link.IsActive)
            .Select(link => new GraphLink(link.Id, link.ParentId, link.ChildId, link.Type, link.BirthOrder));

    private IEnumerable<GraphMarriage> ActiveMarriages() =>
        store.Marriages.Values
            .Where(marriage => marriage.IsActive)
            .Select(marriage => new GraphMarriage(marriage.Id, marriage.FirstSpouseId, marriage.SecondSpouseId, marriage.IsFormer));
}
