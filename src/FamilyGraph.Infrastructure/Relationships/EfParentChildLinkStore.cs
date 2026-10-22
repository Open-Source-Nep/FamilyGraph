using FamilyGraph.Application.Relationships;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;
using FamilyGraph.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FamilyGraph.Infrastructure.Relationships;

internal sealed class EfParentChildLinkStore(FamilyGraphDbContext context) : IParentChildLinkStore
{
    public Task<ParentChildLink?> FindAsync(LinkId id, CancellationToken cancellationToken) =>
        context.Set<ParentChildLink>().SingleOrDefaultAsync(link => link.Id == id, cancellationToken);

    public async Task<IReadOnlySet<PersonId>> ParentsOfAsync(PersonId childId, CancellationToken cancellationToken)
    {
        var parents = await context.Set<ParentChildLink>()
            .AsNoTracking()
            .Where(link => link.ChildId == childId && link.DeletedAt == null)
            .Select(link => link.ParentId)
            .ToListAsync(cancellationToken);
        return parents.ToHashSet();
    }

    /// <summary>
    /// One recursive CTE walks up non-deleted links (D-009). The union is over (person,
    /// generation), so a person reached by many paths is kept once per generation and a cycle
    /// stops at the generation bound. The walk goes one generation past the limit, and the outer
    /// query takes one ancestor past the limit, so either kind of cut shows as an extra row.
    /// </summary>
    public async Task<AncestorSnapshot> AncestorsOfAsync(PersonId personId, CancellationToken cancellationToken)
    {
        var maxGenerations = AncestorSnapshot.MaxGenerations;
        var maxAncestors = AncestorSnapshot.MaxAncestors;
        var rows = await context.Database.SqlQuery<AncestorRow>($"""
            WITH RECURSIVE ancestors (person_id, generation) AS (
                SELECT parent_id, 1
                FROM parent_child_links
                WHERE child_id = {personId.Value} AND deleted_at IS NULL
                UNION
                SELECT link.parent_id, ancestors.generation + 1
                FROM ancestors
                JOIN parent_child_links AS link ON link.child_id = ancestors.person_id
                WHERE link.deleted_at IS NULL AND ancestors.generation <= {maxGenerations}
            )
            SELECT person_id AS "PersonId", MIN(generation) AS "Generation"
            FROM ancestors
            GROUP BY person_id
            ORDER BY MIN(generation), person_id
            LIMIT {maxAncestors + 1}
            """).ToListAsync(cancellationToken);

        var withinLimits = rows.Where(row => row.Generation <= maxGenerations).Take(maxAncestors).ToList();
        var ancestors = withinLimits.Select(row => new PersonId(row.PersonId)).ToHashSet();
        return new AncestorSnapshot(personId, ancestors, isComplete: withinLimits.Count == rows.Count);
    }

    public void Add(ParentChildLink link) => context.Add(link);

    private sealed record AncestorRow(Guid PersonId, int Generation);
}
