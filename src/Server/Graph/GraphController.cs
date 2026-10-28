using FamilyGraph.Application.Graph;
using FamilyGraph.Application.People;
using FamilyGraph.Business.Graph;
using FamilyGraph.Business.People;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.Accounts;

namespace Server.Graph;

/// <summary>
/// Graph exploration (PRD 08, #20): a local view around a person, how two people are connected,
/// the home page's overview of everyone connected to your profiles and a person's Bansawali. All walk only active links and
/// stay within the configured limits (D-009, D-023).
/// </summary>
[ApiController]
[Authorize]
[Route("api/people/{id:guid}")]
public sealed class GraphController(GraphService graph) : ControllerBase
{
    /// <summary>
    /// The person and up to <c>Graph:ViewPeople</c> people nearest to them. "Load more" around an
    /// edge person is this view centred on them. 404 <c>person.not_found</c>, 503 <c>graph.timeout</c>.
    /// </summary>
    [HttpGet("graph")]
    public async Task<LocalViewResponse> LocalView(Guid id, CancellationToken cancellationToken) =>
        LocalViewResponse.From(await graph.LocalViewAsync(ToPersonId(id), User.Viewer(), cancellationToken));

    /// <summary>
    /// Everyone connected to the profiles you maintain, up to <c>Graph:OverviewPeople</c>, for the
    /// home page (D-024). Empty without profiles. 503 <c>graph.timeout</c>.
    /// </summary>
    [HttpGet("/api/graph/overview")]
    public async Task<GraphOverviewResponse> Overview(CancellationToken cancellationToken) =>
        GraphOverviewResponse.From(await graph.OverviewAsync(User.Viewer(), cancellationToken));

    /// <summary>
    /// How the person is connected to another: the shortest path and up to <c>Graph:MorePaths</c>
    /// more (PRD 08). 404 <c>person.not_found</c>, 503 <c>graph.timeout</c>.
    /// </summary>
    [HttpGet("connections/{otherId:guid}")]
    public async Task<ConnectionsResponse> Connections(Guid id, Guid otherId, CancellationToken cancellationToken) =>
        ConnectionsResponse.From(await graph.FindConnectionsAsync(
            ToPersonId(id), ToPersonId(otherId), User.Viewer(), cancellationToken));

    /// <summary>
    /// The person's Bansawali (D-043): their descendants generation by generation, through sons
    /// only (<c>line=Sons</c>, the default) or every child (<c>line=AllChildren</c>), with up to
    /// <c>Graph:BansawaliPeople</c> people and the marriages of each. 400 for another line,
    /// 404 <c>person.not_found</c>, 503 <c>graph.timeout</c>.
    /// </summary>
    [HttpGet("bansawali")]
    public async Task<BansawaliResponse> Bansawali(
        Guid id,
        [FromQuery, RegularExpression("^(Sons|AllChildren)$")] string? line,
        CancellationToken cancellationToken) =>
        BansawaliResponse.From(await graph.BansawaliAsync(
            ToPersonId(id), line is null ? BansawaliLine.Sons : BansawaliLine.FromName(line), User.Viewer(), cancellationToken));

    /// <summary>The empty GUID is never a person, so it is reported like any other unknown ID.</summary>
    private static PersonId ToPersonId(Guid id) =>
        id == Guid.Empty ? throw new PersonNotFoundException() : new PersonId(id);
}
