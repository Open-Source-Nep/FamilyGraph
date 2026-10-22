using FamilyGraph.Application.Relationships;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;
using FamilyGraph.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyGraph.Infrastructure.Relationships;

internal sealed class EfRelationshipQueries(FamilyGraphDbContext context) : IRelationshipQueries
{
    public async Task<DirectRelatives> ListDirectRelativesAsync(PersonId personId, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        // One extra row in each list tells whether it was cut short.
        var parents = await (
                from link in ActiveLinks()
                where link.ChildId == personId
                join parent in context.Set<Person>() on link.ParentId equals parent.Id
                orderby parent.Name.FirstName, parent.Name.MiddleName, parent.Name.LastName, parent.Id
                select new Relative(parent.Id, parent.Name, link.Type))
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var children = await (
                from link in ActiveLinks()
                where link.ParentId == personId
                join child in context.Set<Person>() on link.ChildId equals child.Id
                orderby link.BirthOrder == null, link.BirthOrder, child.Name.FirstName, child.Name.MiddleName,
                    child.Name.LastName, child.Id
                select new ChildRelative(child.Id, child.Name, link.Type, link.BirthOrder))
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        return new DirectRelatives(
            parents.Count > limit ? parents[..limit] : parents,
            parents.Count > limit,
            children.Count > limit ? children[..limit] : children,
            children.Count > limit);
    }

    /// <summary>Not deleted and approved on both sides (PRD 03, D-009).</summary>
    private IQueryable<ParentChildLink> ActiveLinks() =>
        context.Set<ParentChildLink>()
            .AsNoTracking()
            .Where(link => link.DeletedAt == null
                && link.ParentApproval == EndpointApproval.Approved
                && link.ChildApproval == EndpointApproval.Approved);
}
