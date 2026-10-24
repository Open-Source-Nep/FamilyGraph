using FamilyGraph.Business.People;

namespace FamilyGraph.Business.Graph;

/// <summary>
/// Finds how two people are connected through a bounded set of active links and marriages (PRD 08,
/// D-023, D-031). Pure: Application loads the links around the first person within the search limit
/// (D-009), and this service never looks beyond them.
/// </summary>
public static class RelationshipPaths
{
    /// <summary>
    /// Up to <paramref name="maxPaths"/> paths from <paramref name="from"/> to <paramref name="to"/>,
    /// shortest first (Yen's k-shortest simple paths over unweighted links). Paths of equal length
    /// are ordered by the IDs of their links, so the result does not depend on the order of
    /// <paramref name="links"/>. A link set with a cycle is tolerated: no path repeats a person.
    /// A marriage is one step, like a parent–child link (D-031).
    /// </summary>
    public static IReadOnlyList<RelationshipPath> Find(
        PersonId from, PersonId to, IEnumerable<GraphLink> links, int maxPaths) =>
        Find(from, to, links, [], maxPaths);

    /// <inheritdoc cref="Find(PersonId, PersonId, IEnumerable{GraphLink}, int)"/>
    public static IReadOnlyList<RelationshipPath> Find(
        PersonId from, PersonId to, IEnumerable<GraphLink> links, IEnumerable<GraphMarriage> marriages, int maxPaths)
    {
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(marriages);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPaths);

        if (from == to)
        {
            return [new RelationshipPath(from, [])];
        }

        var graph = new Neighbours(links, marriages);
        var shortest = graph.ShortestPath(from, to, new HashSet<PersonId>(), new HashSet<GraphEdge>());
        if (shortest is null)
        {
            return [];
        }

        var found = new List<List<PathStep>> { shortest };
        var candidates = new List<List<PathStep>>();
        while (found.Count < maxPaths)
        {
            var previous = found[^1];
            var previousPeople = new RelationshipPath(from, previous).People.ToList();
            for (var spur = 0; spur < previous.Count; spur++)
            {
                var root = previous[..spur];
                var blockedLinks = found
                    .Where(path => path.Count > spur && path.Take(spur).SequenceEqual(root))
                    .Select(path => path[spur].Link)
                    .ToHashSet();
                var blockedPeople = previousPeople.Take(spur).ToHashSet();

                var spurPath = graph.ShortestPath(previousPeople[spur], to, blockedPeople, blockedLinks);
                if (spurPath is null)
                {
                    continue;
                }

                List<PathStep> candidate = [.. root, .. spurPath];
                if (!found.Any(path => SameLinks(path, candidate)) && !candidates.Any(path => SameLinks(path, candidate)))
                {
                    candidates.Add(candidate);
                }
            }

            if (candidates.Count == 0)
            {
                break;
            }

            var next = candidates.OrderBy(path => path.Count).ThenBy(Key, StringComparer.Ordinal).First();
            candidates.Remove(next);
            found.Add(next);
        }

        return [.. found.Select(steps => new RelationshipPath(from, steps))];
    }

    private static bool SameLinks(List<PathStep> left, List<PathStep> right) =>
        left.Select(step => step.Link.Id).SequenceEqual(right.Select(step => step.Link.Id));

    private static string Key(List<PathStep> path) => string.Join(',', path.Select(step => step.Link.Id.Value.ToString("N")));

    /// <summary>
    /// Each person's links in a fixed order: to parents first, then to children, then to spouses,
    /// each by link ID.
    /// </summary>
    private sealed class Neighbours
    {
        private readonly Dictionary<PersonId, List<PathStep>> _steps = [];

        public Neighbours(IEnumerable<GraphLink> links, IEnumerable<GraphMarriage> marriages)
        {
            foreach (var link in links.DistinctBy(link => link.Id))
            {
                StepsFrom(link.ChildId).Add(new PathStep(link, StepRelation.Parent, link.ParentId));
                StepsFrom(link.ParentId).Add(new PathStep(link, StepRelation.Child, link.ChildId));
            }

            foreach (var marriage in marriages.DistinctBy(marriage => marriage.Id))
            {
                StepsFrom(marriage.FirstSpouseId).Add(new PathStep(marriage, StepRelation.Spouse, marriage.SecondSpouseId));
                StepsFrom(marriage.SecondSpouseId).Add(new PathStep(marriage, StepRelation.Spouse, marriage.FirstSpouseId));
            }

            foreach (var steps in _steps.Values)
            {
                steps.Sort((left, right) => left.Relation.Value != right.Relation.Value
                    ? left.Relation.Value.CompareTo(right.Relation.Value)
                    : left.Link.Id.Value.CompareTo(right.Link.Id.Value));
            }
        }

        /// <summary>Breadth-first search that never enters <paramref name="blockedPeople"/> or follows <paramref name="blockedLinks"/>.</summary>
        public List<PathStep>? ShortestPath(
            PersonId from, PersonId to, IReadOnlySet<PersonId> blockedPeople, IReadOnlySet<GraphEdge> blockedLinks)
        {
            var reachedBy = new Dictionary<PersonId, PathStep?> { [from] = null };
            var queue = new Queue<PersonId>([from]);
            while (queue.TryDequeue(out var person))
            {
                if (person == to)
                {
                    return StepsTo(to, reachedBy);
                }

                foreach (var step in _steps.GetValueOrDefault(person, []))
                {
                    if (!blockedLinks.Contains(step.Link)
                        && !blockedPeople.Contains(step.PersonId)
                        && reachedBy.TryAdd(step.PersonId, step))
                    {
                        queue.Enqueue(step.PersonId);
                    }
                }
            }

            return null;
        }

        private static List<PathStep> StepsTo(PersonId to, Dictionary<PersonId, PathStep?> reachedBy)
        {
            var steps = new List<PathStep>();
            var person = to;
            while (reachedBy[person] is { } step)
            {
                steps.Add(step);
                person = step.Link switch
                {
                    GraphLink link => link.ParentId == step.PersonId ? link.ChildId : link.ParentId,
                    GraphMarriage marriage => marriage.FirstSpouseId == step.PersonId ? marriage.SecondSpouseId : marriage.FirstSpouseId,
                    _ => throw new InvalidOperationException("Unknown kind of graph edge."),
                };
            }

            steps.Reverse();
            return steps;
        }

        private List<PathStep> StepsFrom(PersonId person)
        {
            if (!_steps.TryGetValue(person, out var steps))
            {
                steps = [];
                _steps[person] = steps;
            }

            return steps;
        }
    }
}
