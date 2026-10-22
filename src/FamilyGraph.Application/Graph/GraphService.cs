using FamilyGraph.Application.People;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.Graph;
using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Graph;

/// <summary>
/// Explores the active graph around a person and finds how two people are connected (PRD 08, #20).
/// Any signed-in account may, like viewing a profile (D-019). Every person returned goes through
/// <see cref="PrivacyProjection"/> (D-010).
/// </summary>
public sealed class GraphService(IPersonQueries people, IGraphQueries queries, GraphLimits limits)
{
    /// <summary>
    /// The centre and the people nearest to it, within the configured generations and people limit
    /// (D-009, D-023). A "load more" around an edge person is the local view centred on them.
    /// </summary>
    public async Task<LocalView> LocalViewAsync(PersonId centreId, AccountId viewerId, CancellationToken cancellationToken)
    {
        _ = await people.FindAsync(centreId, viewerId, cancellationToken) ?? throw new PersonNotFoundException();
        var view = await queries.LocalViewAsync(
            centreId, viewerId, limits.ViewGenerations, limits.ViewPeople, cancellationToken);

        return new LocalView(
            centreId,
            [.. view.People.Select(row =>
                new LocalViewPerson(PrivacyProjection.Project(row.Person), row.Generation, row.HasMore))],
            view.Links,
            view.IsTruncated);
    }

    /// <summary>
    /// Everyone connected through active links to the profiles the viewer maintains, in one bounded
    /// walk of at most <see cref="GraphLimits.OverviewPeople"/> people (D-023, D-024). Without
    /// profiles it is empty.
    /// </summary>
    public async Task<Overview> OverviewAsync(AccountId viewerId, CancellationToken cancellationToken)
    {
        var profiles = await people.ListMaintainedByAsync(viewerId, limits.OverviewPeople, cancellationToken);
        if (profiles.People.Count == 0)
        {
            return new Overview([], [], false);
        }

        var overview = await queries.OverviewAsync(
            [.. profiles.People.Select(profile => profile.Id)], viewerId, limits.OverviewPeople, cancellationToken);
        return new Overview(
            [.. overview.People.Select(PrivacyProjection.Project)],
            overview.Links,
            overview.IsTruncated || profiles.IsTruncated);
    }

    /// <summary>
    /// The shortest path from one person to another and up to <see cref="GraphLimits.MorePaths"/>
    /// further paths, within the people examined from the first person (D-009, D-023).
    /// </summary>
    public async Task<Connections> FindConnectionsAsync(
        PersonId fromId, PersonId toId, AccountId viewerId, CancellationToken cancellationToken)
    {
        _ = await people.FindAsync(fromId, viewerId, cancellationToken) ?? throw new PersonNotFoundException();
        _ = await people.FindAsync(toId, viewerId, cancellationToken) ?? throw new PersonNotFoundException();

        var neighbourhood = await queries.LinksAroundAsync(fromId, limits.PathSearchPeople, cancellationToken);
        // One path past the listed ones tells whether more exist.
        var paths = RelationshipPaths.Find(fromId, toId, neighbourhood.Links, limits.MorePaths + 2);
        var listed = paths.Take(limits.MorePaths + 1).ToList();

        var ids = listed.SelectMany(path => path.People).ToHashSet();
        var records = await queries.PeopleAsync(ids, viewerId, cancellationToken);
        var visible = records.ToDictionary(record => record.Id, PrivacyProjection.Project);

        return new Connections(
            [.. listed.Select(path => new Connection(
                visible[path.Start],
                [.. path.Steps.Select(step => new ConnectionStep(step.Link, step.Relation, visible[step.PersonId]))]))],
            paths.Count > listed.Count,
            neighbourhood.IsComplete);
    }
}
