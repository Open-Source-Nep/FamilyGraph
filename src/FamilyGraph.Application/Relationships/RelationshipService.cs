using FamilyGraph.Application.People;
using FamilyGraph.Application.Persistence;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Application.Relationships;

/// <summary>
/// Creates parent–child links and lists a person's parents and children (PRD 02, #14).
/// <see cref="ParentChildLink"/> enforces who may create a link, the per-endpoint starting
/// approvals, duplicates and cycles; this service loads what it needs to check them.
/// </summary>
public sealed class RelationshipService(
    IPersonStore people,
    IPersonQueries personQueries,
    IParentChildLinkStore links,
    IRelationshipQueries queries,
    IAuditLog audit,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    RelationshipLimits limits)
{
    public async Task<CreatedLink> CreateLinkAsync(
        PersonId parentId,
        PersonId childId,
        ParentChildType type,
        AccountId actorId,
        CancellationToken cancellationToken)
    {
        var parent = await people.FindAsync(parentId, cancellationToken) ?? throw new PersonNotFoundException();
        var child = await people.FindAsync(childId, cancellationToken) ?? throw new PersonNotFoundException();
        var childParents = await links.ParentsOfAsync(childId, cancellationToken);
        var parentAncestors = await links.AncestorsOfAsync(parentId, cancellationToken);

        var link = ParentChildLink.Create(parent, child, type, actorId, childParents, parentAncestors);
        links.Add(link);
        audit.Add(link.TakeEvents(), time.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreatedLink(link.Id, link.ParentId, link.ChildId, link.Type, link.IsActive);
    }

    /// <summary>Any signed-in account may list them, like viewing a profile (D-019).</summary>
    public async Task<DirectRelatives> ListDirectRelativesAsync(
        PersonId personId,
        AccountId viewerId,
        CancellationToken cancellationToken)
    {
        _ = await personQueries.FindAsync(personId, viewerId, cancellationToken) ?? throw new PersonNotFoundException();
        return await queries.ListDirectRelativesAsync(personId, limits.DirectRelativesLimit, cancellationToken);
    }
}
