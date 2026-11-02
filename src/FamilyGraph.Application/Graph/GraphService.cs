using FamilyGraph.Application.People;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.Graph;
using FamilyGraph.Business.Kinship;
using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Graph;

/// <summary>
/// Explores the active graph around a person and finds how two people are connected (PRD 08, #20).
/// Any signed-in account may, like viewing a profile (D-019). Every person returned goes through
/// <see cref="PrivacyProjection"/> (D-010): placeholders keep their place in the graph, so walks
/// and paths pass through them (PRD 05).
/// </summary>
public sealed class GraphService(IPersonQueries people, IGraphQueries queries, GraphLimits limits)
{
    /// <summary>
    /// The centre and the people nearest to it, within the configured generations and people limit
    /// (D-009, D-023). A "load more" around an edge person is the local view centred on them.
    /// </summary>
    public async Task<LocalView> LocalViewAsync(PersonId centreId, Viewer viewer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        var viewerId = viewer.AccountId;
        _ = await people.FindAsync(centreId, viewerId, cancellationToken) ?? throw new PersonNotFoundException();
        var view = await queries.LocalViewAsync(
            centreId, viewerId, limits.ViewGenerations, limits.ViewPeople, cancellationToken);

        return new LocalView(
            centreId,
            [.. view.People.Select(row =>
                new LocalViewPerson(PrivacyProjection.Project(row.Person, viewer), row.Generation, row.HasMore))],
            view.Links,
            view.Marriages,
            view.IsTruncated);
    }

    /// <summary>
    /// Everyone connected through active links to the profiles the viewer maintains, in one bounded
    /// walk of at most <see cref="GraphLimits.OverviewPeople"/> people (D-023, D-024). Without
    /// profiles it is empty.
    /// </summary>
    public async Task<Overview> OverviewAsync(Viewer viewer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        var viewerId = viewer.AccountId;
        var profiles = await people.ListMaintainedByAsync(viewerId, limits.OverviewPeople, cancellationToken);
        if (profiles.People.Count == 0)
        {
            return new Overview([], [], [], false);
        }

        var overview = await queries.OverviewAsync(
            [.. profiles.People.Select(profile => profile.Id)], viewerId, limits.OverviewPeople, cancellationToken);
        return new Overview(
            [.. overview.People.Select(person => PrivacyProjection.Project(person, viewer))],
            overview.Links,
            overview.Marriages,
            overview.IsTruncated || profiles.IsTruncated);
    }

    /// <summary>
    /// The shortest path from one person to another and up to <see cref="GraphLimits.MorePaths"/>
    /// further paths, within the people examined from the first person (D-009, D-023). With a
    /// <paramref name="culture"/>, the shortest path also gets that culture's label for the second
    /// person as seen from the first (PRD 08, D-048), decided only from what the viewer may see.
    /// </summary>
    public async Task<Connections> FindConnectionsAsync(
        PersonId fromId, PersonId toId, Viewer viewer, KinshipCulture? culture, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        var viewerId = viewer.AccountId;
        _ = await people.FindAsync(fromId, viewerId, cancellationToken) ?? throw new PersonNotFoundException();
        _ = await people.FindAsync(toId, viewerId, cancellationToken) ?? throw new PersonNotFoundException();

        var neighbourhood = await queries.LinksAroundAsync(fromId, limits.PathSearchPeople, cancellationToken);
        // One path past the listed ones tells whether more exist.
        var paths = RelationshipPaths.Find(fromId, toId, neighbourhood.Links, neighbourhood.Marriages, limits.MorePaths + 2);
        var listed = paths.Take(limits.MorePaths + 1).ToList();

        var ids = listed.SelectMany(path => path.People).ToHashSet();
        var records = await queries.PeopleAsync(ids, viewerId, cancellationToken);
        var visible = records.ToDictionary(record => record.Id, record => PrivacyProjection.Project(record, viewer));

        var label = culture is not null && listed.Count > 0
            ? culture.Label(await KinshipFactsAsync(listed[0], neighbourhood.Links, visible, viewer, cancellationToken))
            : null;

        return new Connections(
            [.. listed.Select(path => new Connection(
                visible[path.Start],
                [.. path.Steps.Select(step => new ConnectionStep(step.Link, step.Relation, visible[step.PersonId]))]))],
            paths.Count > listed.Count,
            neighbourhood.IsComplete,
            label);
    }

    /// <summary>
    /// What label rules may test about <paramref name="path"/> (D-048). It loads the children of
    /// each parent the path walks down from, and of the other parents of the person before such a
    /// turn, so siblings can be ranked and compared, and projects those children so a hidden
    /// gender stays unknown. A parent with more children than the people limit is not ranked.
    /// </summary>
    private async Task<KinshipFacts> KinshipFactsAsync(
        RelationshipPath path,
        IReadOnlyList<GraphLink> around,
        Dictionary<PersonId, PersonReference> visible,
        Viewer viewer,
        CancellationToken cancellationToken)
    {
        var people = path.People.ToList();
        var turning = new HashSet<PersonId>();
        for (var index = 0; index < path.Steps.Count; index++)
        {
            if (path.Steps[index].Relation != StepRelation.Child)
            {
                continue;
            }
            turning.Add(people[index]);
            if (index > 0 && path.Steps[index - 1].Relation == StepRelation.Parent)
            {
                turning.UnionWith(around.Where(link => link.ChildId == people[index - 1]).Select(link => link.ParentId));
            }
        }

        var links = around.ToList();
        var complete = new HashSet<PersonId>();
        if (turning.Count > 0)
        {
            var children = await queries.ChildLinksAsync(turning, limits.PathSearchPeople, cancellationToken);
            if (!children.IsTruncated)
            {
                links = [.. around.Concat(children.Links).DistinctBy(link => link.Id)];
                complete.UnionWith(turning);
                var unseen = children.Links.Select(link => link.ChildId).Where(id => !visible.ContainsKey(id)).ToHashSet();
                foreach (var record in await queries.PeopleAsync(unseen, viewer.AccountId, cancellationToken))
                {
                    visible[record.Id] = PrivacyProjection.Project(record, viewer);
                }
            }
        }
        return KinshipFacts.Of(path, id => visible.GetValueOrDefault(id)?.Gender, links, complete);
    }

    /// <summary>
    /// The Bansawali of an ancestor (PRD 08, D-043): their descendants generation by generation,
    /// following only the children that <paramref name="line"/> continues through. The ancestor's
    /// own children are always listed. Each child is projected before the line decides, so a
    /// hidden gender counts as not set and the shape leaks nothing (PRD 05). A child of two
    /// people in the line appears once, under the first. When the next generation would pass
    /// <see cref="GraphLimits.BansawaliPeople"/>, it is left out and the people whose children
    /// were cut off get <see cref="BansawaliPerson.HasMore"/>; their own Bansawali loads the rest.
    /// </summary>
    public async Task<Bansawali> BansawaliAsync(
        PersonId rootId, BansawaliLine line, Viewer viewer, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(viewer);
        var viewerId = viewer.AccountId;
        _ = await people.FindAsync(rootId, viewerId, cancellationToken) ?? throw new PersonNotFoundException();
        var root = Found(await queries.PeopleAsync([rootId], viewerId, cancellationToken), rootId);

        var shown = new List<(PersonReference Person, int Generation, bool Continues)>
        {
            (PrivacyProjection.Project(root, viewer), 0, true),
        };
        var seen = new HashSet<PersonId> { rootId };
        var links = new List<GraphLink>();
        var withMore = new HashSet<PersonId>();
        var isTruncated = false;
        List<PersonId> parents = [rootId];
        for (var generation = 1; parents.Count > 0; generation++)
        {
            var children = await queries.ChildLinksAsync(parents, limits.BansawaliPeople - (shown.Count - 1), cancellationToken);
            if (children.IsTruncated)
            {
                withMore.UnionWith(await queries.WithChildrenAsync(parents, cancellationToken));
                isTruncated = true;
                break;
            }

            var placing = children.Links.Where(link => seen.Add(link.ChildId)).ToList();
            var records = (await queries.PeopleAsync([.. placing.Select(link => link.ChildId)], viewerId, cancellationToken))
                .ToDictionary(record => record.Id);
            parents = [];
            foreach (var link in placing)
            {
                var child = PrivacyProjection.Project(records[link.ChildId], viewer);
                var continues = line.Continues(link.Type, child.Gender);
                shown.Add((child, generation, continues));
                links.Add(link);
                if (continues)
                {
                    parents.Add(child.Id);
                }
            }
        }

        var marriages = await queries.MarriagesOfAsync([.. seen], limits.BansawaliPeople, cancellationToken);
        var spouseIds = marriages.Marriages
            .SelectMany(marriage => new[] { marriage.FirstSpouseId, marriage.SecondSpouseId })
            .Where(id => !seen.Contains(id))
            .ToHashSet();
        var spouses = await queries.PeopleAsync(spouseIds, viewerId, cancellationToken);

        return new Bansawali(
            rootId,
            line,
            [.. shown.Select(row => new BansawaliPerson(row.Person, row.Generation, row.Continues, withMore.Contains(row.Person.Id)))],
            links,
            [.. spouses.Select(spouse => PrivacyProjection.Project(spouse, viewer))],
            marriages.Marriages,
            isTruncated || marriages.IsTruncated);
    }

    private static PersonRecord Found(IReadOnlyList<PersonRecord> records, PersonId id) =>
        records.SingleOrDefault(record => record.Id == id) ?? throw new PersonNotFoundException();
}
