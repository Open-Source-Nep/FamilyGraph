using FamilyGraph.Application.Relationships;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;
using FamilyGraph.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyGraph.Infrastructure.Relationships;

internal sealed class EfRelationshipQueries(FamilyGraphDbContext context) : IRelationshipQueries
{
    public async Task<DirectRelatives> ListDirectRelativesAsync(
        PersonId personId, AccountId viewerId, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        // One extra row in each list tells whether it was cut short.
        var parents = await (
                from link in ActiveLinks()
                where link.ChildId == personId
                join parent in context.Set<Person>() on link.ParentId equals parent.Id
                orderby parent.Name.FirstName, parent.Name.MiddleName, parent.Name.LastName, parent.Id
                select new Relative(link.Id, parent.Id, parent.Name, link.Type, parent.MaintainerId == viewerId))
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var children = await (
                from link in ActiveLinks()
                where link.ParentId == personId
                join child in context.Set<Person>() on link.ChildId equals child.Id
                orderby link.BirthOrder == null, link.BirthOrder, child.Name.FirstName, child.Name.MiddleName,
                    child.Name.LastName, child.Id
                select new ChildRelative(
                    link.Id, child.Id, child.Name, link.Type, child.MaintainerId == viewerId, link.BirthOrder))
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        return new DirectRelatives(
            parents.Count > limit ? parents[..limit] : parents,
            parents.Count > limit,
            children.Count > limit ? children[..limit] : children,
            children.Count > limit);
    }

    public async Task<LinkConsentList> ListInactiveLinksAsync(
        AccountId viewerId, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        var rows = await (
                from link in context.Set<ParentChildLink>().AsNoTracking()
                where link.DeletedAt == null
                    && !(link.ParentApproval == EndpointApproval.Approved && link.ChildApproval == EndpointApproval.Approved)
                join parent in context.Set<Person>() on link.ParentId equals parent.Id
                join child in context.Set<Person>() on link.ChildId equals child.Id
                where parent.MaintainerId == viewerId || child.MaintainerId == viewerId
                let awaitingViewer = (parent.MaintainerId == viewerId && link.ParentApproval == EndpointApproval.Pending)
                    || (child.MaintainerId == viewerId && link.ChildApproval == EndpointApproval.Pending)
                orderby awaitingViewer descending, parent.Name.FirstName, parent.Name.MiddleName, parent.Name.LastName,
                    child.Name.FirstName, child.Name.MiddleName, child.Name.LastName, link.Id
                select new LinkConsentView(
                    link.Id,
                    link.Type,
                    new LinkEndpointView(parent.Id, parent.Name, link.ParentApproval, parent.MaintainerId == viewerId),
                    new LinkEndpointView(child.Id, child.Name, link.ChildApproval, child.MaintainerId == viewerId),
                    false))
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        return new LinkConsentList(rows.Count > limit ? rows[..limit] : rows, rows.Count > limit);
    }

    /// <summary>Not deleted and approved on both sides (PRD 03, D-009).</summary>
    private IQueryable<ParentChildLink> ActiveLinks() =>
        context.Set<ParentChildLink>()
            .AsNoTracking()
            .Where(link => link.DeletedAt == null
                && link.ParentApproval == EndpointApproval.Approved
                && link.ChildApproval == EndpointApproval.Approved);
}
