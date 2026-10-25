using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Application.Relationships;

/// <summary>
/// Read models of parent–child links (D-002). Relationship views return only active links
/// (PRD 03); the consent view returns the inactive links of the viewer's own profiles (D-021).
/// People come back as <see cref="PersonRecord"/>s for <see cref="PrivacyProjection"/>. Lists
/// sorted by name put people hidden from the viewer (deleted, or Private and not theirs) last, by
/// ID, so their place in the list reveals nothing about their name (D-025).
/// </summary>
public interface IRelationshipQueries
{
    /// <summary>
    /// The person's parents, sorted by name, and children, in birth order with unordered children
    /// after them by name (D-016). Each list holds at most <paramref name="limit"/> people (D-010).
    /// </summary>
    Task<DirectRelativesData> ListDirectRelativesAsync(
        PersonId personId, Viewer viewer, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// The non-deleted, inactive links with at least one non-deleted endpoint the viewer maintains
    /// (a deleted profile's consent is frozen, D-026), those
    /// waiting for the viewer's approval first, then by parent and child name. At most
    /// <paramref name="limit"/> links (D-010).
    /// </summary>
    Task<LinkConsentListData> ListInactiveLinksAsync(Viewer viewer, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Every non-deleted link with a pending endpoint whose person is not deleted and is maintained
    /// by someone other than the viewer, by parent and child name (D-040). At most
    /// <paramref name="limit"/> links.
    /// </summary>
    Task<LinkConsentListData> ListPendingLinksAsync(Viewer viewer, int limit, CancellationToken cancellationToken);
}

/// <summary>A relative before privacy is applied. <see cref="BirthOrder"/> is only set for children (D-016).</summary>
public sealed record RelativeData(LinkId LinkId, PersonRecord Person, ParentChildType Type, int? BirthOrder);

public sealed record DirectRelativesData(
    IReadOnlyList<RelativeData> Parents,
    bool ParentsTruncated,
    IReadOnlyList<RelativeData> Children,
    bool ChildrenTruncated);

public sealed record LinkEndpointData(PersonRecord Person, EndpointApproval Approval);

public sealed record LinkConsentData(
    LinkId Id, ParentChildType Type, LinkEndpointData Parent, LinkEndpointData Child, bool IsActive);

public sealed record LinkConsentListData(IReadOnlyList<LinkConsentData> Links, bool IsTruncated);
