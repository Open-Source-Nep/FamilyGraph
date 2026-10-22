using FamilyGraph.Application.People;
using FamilyGraph.Application.Persistence;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Application.Relationships;

/// <summary>
/// Creates parent–child links, changes an endpoint's approval, deletes links and lists a person's
/// parents and children (PRD 02, PRD 03, #14, #59, #61). <see cref="ParentChildLink"/> enforces who may create or
/// approve a link, the per-endpoint approvals, duplicates and cycles; this service loads what it
/// needs to check them and saves each change with its audit events.
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
        await SaveAsync(link, cancellationToken);

        return new CreatedLink(link.Id, link.ParentId, link.ChildId, link.Type, link.IsActive);
    }

    /// <summary>
    /// Approves the link for one endpoint, from pending or disagreed (D-013). If this makes the
    /// link active, the domain re-runs the bounded cycle check first (PRD 02, AC-02.6).
    /// </summary>
    public async Task<LinkConsentView> ApproveAsync(
        LinkId linkId,
        LinkEndpoint endpoint,
        AccountId actorId,
        CancellationToken cancellationToken)
    {
        var (link, parent, child) = await LoadAsync(linkId, cancellationToken);
        var parentAncestors = await links.AncestorsOfAsync(link.ParentId, cancellationToken);

        link.Approve(endpoint == LinkEndpoint.Parent ? parent : child, actorId, parentAncestors);
        await SaveAsync(link, cancellationToken);

        return LinkConsentView.From(link, parent, child, actorId);
    }

    /// <summary>
    /// Declines a pending endpoint or withdraws an approval (D-013). The link is inactive at once.
    /// </summary>
    public async Task<LinkConsentView> DisagreeAsync(
        LinkId linkId,
        LinkEndpoint endpoint,
        AccountId actorId,
        CancellationToken cancellationToken)
    {
        var (link, parent, child) = await LoadAsync(linkId, cancellationToken);

        if (endpoint == LinkEndpoint.Parent)
        {
            link.Disagree(parent, child, actorId);
        }
        else
        {
            link.Disagree(child, parent, actorId);
        }

        await SaveAsync(link, cancellationToken);

        return LinkConsentView.From(link, parent, child, actorId);
    }

    /// <summary>
    /// Deletes the link, in any consent state, when the actor maintains both endpoints (D-014).
    /// Deletion is final; the link stays stored for its history and leaves every list.
    /// </summary>
    public async Task DeleteAsync(LinkId linkId, AccountId actorId, CancellationToken cancellationToken)
    {
        var (link, parent, child) = await LoadAsync(linkId, cancellationToken);

        link.Delete(parent, child, actorId, time.GetUtcNow());
        await SaveAsync(link, cancellationToken);
    }

    /// <summary>The inactive links of the profiles the account maintains (D-021).</summary>
    public Task<LinkConsentList> ListInactiveLinksAsync(AccountId viewerId, CancellationToken cancellationToken) =>
        queries.ListInactiveLinksAsync(viewerId, limits.InactiveLinksLimit, cancellationToken);

    /// <summary>Any signed-in account may list them, like viewing a profile (D-019).</summary>
    public async Task<DirectRelatives> ListDirectRelativesAsync(
        PersonId personId,
        AccountId viewerId,
        CancellationToken cancellationToken)
    {
        _ = await personQueries.FindAsync(personId, viewerId, cancellationToken) ?? throw new PersonNotFoundException();
        return await queries.ListDirectRelativesAsync(personId, viewerId, limits.DirectRelativesLimit, cancellationToken);
    }

    private async Task<(ParentChildLink Link, Person Parent, Person Child)> LoadAsync(
        LinkId linkId,
        CancellationToken cancellationToken)
    {
        var link = await links.FindAsync(linkId, cancellationToken) ?? throw new LinkNotFoundException();
        var parent = await people.FindAsync(link.ParentId, cancellationToken) ?? throw new PersonNotFoundException();
        var child = await people.FindAsync(link.ChildId, cancellationToken) ?? throw new PersonNotFoundException();
        return (link, parent, child);
    }

    /// <summary>Saves the change and its audit events in one transaction (D-002, D-015).</summary>
    private async Task SaveAsync(ParentChildLink link, CancellationToken cancellationToken)
    {
        audit.Add(link.TakeEvents(), time.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
