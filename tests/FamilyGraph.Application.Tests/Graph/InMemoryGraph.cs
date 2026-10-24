using FamilyGraph.Application.Graph;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Application.Tests.People;
using FamilyGraph.Business.Graph;
using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Tests.Graph;

/// <summary>
/// Graph queries over the in-memory people and links: a breadth-first walk of the active links with
/// the same order and bounds as the database query (D-023). It records the limits it was given.
/// </summary>
internal sealed class InMemoryGraph(InMemoryPeople store) : IGraphQueries
{
    public (int Generations, int PeopleLimit)? LastViewLimits { get; private set; }

    public int? LastSearchLimit { get; private set; }

    public int? LastOverviewLimit { get; private set; }

    /// <summary>Makes every connection search report that it stopped at its people limit.</summary>
    public bool SearchHitsLimit { get; set; }

    public Task<LocalViewData> LocalViewAsync(
        PersonId centreId, AccountId viewerId, int generations, int peopleLimit, CancellationToken cancellationToken)
    {
        LastViewLimits = (generations, peopleLimit);
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
        LastSearchLimit = peopleLimit;
        return Task.FromResult(new LinkNeighbourhood([.. ActiveLinks()], [.. ActiveMarriages()], !SearchHitsLimit));
    }

    public Task<OverviewData> OverviewAsync(
        IReadOnlyCollection<PersonId> startIds, AccountId viewerId, int peopleLimit, CancellationToken cancellationToken)
    {
        LastOverviewLimit = peopleLimit;
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

    public Task<IReadOnlyList<PersonRecord>> PeopleAsync(
        IReadOnlyCollection<PersonId> ids, AccountId viewerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PersonRecord>>(
            [.. ids.Where(store.People.ContainsKey).Select(id => Record(id, viewerId))]);

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
            .Select(link => new GraphLink(link.Id, link.ParentId, link.ChildId, link.Type));

    private IEnumerable<GraphMarriage> ActiveMarriages() =>
        store.Marriages.Values
            .Where(marriage => marriage.IsActive)
            .Select(marriage => new GraphMarriage(marriage.Id, marriage.FirstSpouseId, marriage.SecondSpouseId, marriage.IsFormer));
}
