using FamilyGraph.Application.Privacy;
using FamilyGraph.Application.Relationships;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;
using FamilyGraph.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyGraph.Infrastructure.Relationships;

/// <summary>
/// People hidden from the viewer sort after the others and by ID only, with an empty name in the
/// sort key, so the order cannot hint at a hidden name (D-025). The rule matches
/// <see cref="PrivacyProjection"/>: deleted, or Private, not maintained by the viewer, and the
/// viewer is not an administrator.
/// </summary>
internal sealed class EfRelationshipQueries(FamilyGraphDbContext context) : IRelationshipQueries
{
    public async Task<DirectRelativesData> ListDirectRelativesAsync(
        PersonId personId, Viewer viewer, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        var viewerId = viewer.AccountId;
        var seesAll = viewer.IsAdministrator;

        // One extra row in each list tells whether it was cut short.
        var parents = await (
                from link in ActiveLinks()
                where link.ChildId == personId
                join parent in context.Set<Person>() on link.ParentId equals parent.Id
                let hidden = parent.DeletedAt != null
                    || (!seesAll && parent.Visibility == ProfileVisibility.Private && parent.MaintainerId != viewerId)
                orderby hidden, hidden ? "" : parent.Name.FirstName, hidden ? "" : parent.Name.MiddleName,
                    hidden ? "" : parent.Name.LastName, parent.Id
                select new RelativeData(
                    link.Id,
                    new PersonRecord(parent.Id, parent.Name, parent.Visibility, parent.DeletedAt != null, parent.MaintainerId == viewerId, parent.ImageId, parent.Gender),
                    link.Type,
                    null))
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var children = await (
                from link in ActiveLinks()
                where link.ParentId == personId
                join child in context.Set<Person>() on link.ChildId equals child.Id
                let hidden = child.DeletedAt != null
                    || (!seesAll && child.Visibility == ProfileVisibility.Private && child.MaintainerId != viewerId)
                orderby link.BirthOrder == null, link.BirthOrder, hidden, hidden ? "" : child.Name.FirstName,
                    hidden ? "" : child.Name.MiddleName, hidden ? "" : child.Name.LastName, child.Id
                select new RelativeData(
                    link.Id,
                    new PersonRecord(child.Id, child.Name, child.Visibility, child.DeletedAt != null, child.MaintainerId == viewerId, child.ImageId, child.Gender),
                    link.Type,
                    link.BirthOrder))
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        return new DirectRelativesData(
            parents.Count > limit ? parents[..limit] : parents,
            parents.Count > limit,
            children.Count > limit ? children[..limit] : children,
            children.Count > limit);
    }

    public async Task<LinkConsentListData> ListInactiveLinksAsync(
        Viewer viewer, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        var viewerId = viewer.AccountId;
        var seesAll = viewer.IsAdministrator;

        var rows = await (
                from link in context.Set<ParentChildLink>().AsNoTracking()
                where link.DeletedAt == null
                    && !(link.ParentApproval == EndpointApproval.Approved && link.ChildApproval == EndpointApproval.Approved)
                join parent in context.Set<Person>() on link.ParentId equals parent.Id
                join child in context.Set<Person>() on link.ChildId equals child.Id
                let ownsParent = parent.MaintainerId == viewerId && parent.DeletedAt == null
                let ownsChild = child.MaintainerId == viewerId && child.DeletedAt == null
                where ownsParent || ownsChild
                let awaitingViewer = (ownsParent && link.ParentApproval == EndpointApproval.Pending)
                    || (ownsChild && link.ChildApproval == EndpointApproval.Pending)
                let parentHidden = parent.DeletedAt != null
                    || (!seesAll && parent.Visibility == ProfileVisibility.Private && parent.MaintainerId != viewerId)
                let childHidden = child.DeletedAt != null
                    || (!seesAll && child.Visibility == ProfileVisibility.Private && child.MaintainerId != viewerId)
                orderby awaitingViewer descending, parentHidden, parentHidden ? "" : parent.Name.FirstName,
                    parentHidden ? "" : parent.Name.MiddleName, parentHidden ? "" : parent.Name.LastName,
                    childHidden, childHidden ? "" : child.Name.FirstName, childHidden ? "" : child.Name.MiddleName,
                    childHidden ? "" : child.Name.LastName, link.Id
                select new LinkConsentData(
                    link.Id,
                    link.Type,
                    new LinkEndpointData(
                        new PersonRecord(parent.Id, parent.Name, parent.Visibility, parent.DeletedAt != null, parent.MaintainerId == viewerId, parent.ImageId, parent.Gender),
                        link.ParentApproval),
                    new LinkEndpointData(
                        new PersonRecord(child.Id, child.Name, child.Visibility, child.DeletedAt != null, child.MaintainerId == viewerId, child.ImageId, child.Gender),
                        link.ChildApproval),
                    false))
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        return new LinkConsentListData(rows.Count > limit ? rows[..limit] : rows, rows.Count > limit);
    }

    /// <summary>Not deleted and approved on both sides (PRD 03, D-009).</summary>
    private IQueryable<ParentChildLink> ActiveLinks() =>
        context.Set<ParentChildLink>()
            .AsNoTracking()
            .Where(link => link.DeletedAt == null
                && link.ParentApproval == EndpointApproval.Approved
                && link.ChildApproval == EndpointApproval.Approved);
}
