using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Application.Relationships;

/// <summary>
/// A newly created link as its creator sees it. <see cref="IsActive"/> is false while the other
/// endpoint's maintainer has not approved it (PRD 03).
/// </summary>
public sealed record CreatedLink(LinkId Id, PersonId ParentId, PersonId ChildId, ParentChildType Type, bool IsActive);

/// <summary>
/// A parent or child through an active link, with the link's ID and type (PRD 02).
/// <see cref="MaintainedByViewer"/> says whether the viewer maintains the relative, never who does (D-019).
/// </summary>
public sealed record Relative(LinkId LinkId, PersonId Id, PersonName Name, ParentChildType Type, bool MaintainedByViewer);

/// <summary>A child also carries its birth-order number, or <see langword="null"/> while unordered (D-016).</summary>
public sealed record ChildRelative(
    LinkId LinkId, PersonId Id, PersonName Name, ParentChildType Type, bool MaintainedByViewer, int? BirthOrder);

/// <summary>A person's parents and children through active links. Each list says whether it was cut at its limit (D-010).</summary>
public sealed record DirectRelatives(
    IReadOnlyList<Relative> Parents,
    bool ParentsTruncated,
    IReadOnlyList<ChildRelative> Children,
    bool ChildrenTruncated);

/// <summary>One side of a link in the consent view: the person, its approval and whether the viewer maintains it.</summary>
public sealed record LinkEndpointView(PersonId Id, PersonName Name, EndpointApproval Approval, bool MaintainedByViewer);

/// <summary>
/// A link's consent state as a maintainer of one of its endpoints sees it (D-021). PRD 07 lets
/// the current maintainers of both profiles see the link's history, so both approvals are shown.
/// </summary>
public sealed record LinkConsentView(
    LinkId Id, ParentChildType Type, LinkEndpointView Parent, LinkEndpointView Child, bool IsActive)
{
    internal static LinkConsentView From(ParentChildLink link, Person parent, Person child, AccountId viewerId) => new(
        link.Id,
        link.Type,
        new LinkEndpointView(parent.Id, parent.Name, link.ParentApproval, parent.IsMaintainedBy(viewerId)),
        new LinkEndpointView(child.Id, child.Name, link.ChildApproval, child.IsMaintainedBy(viewerId)),
        link.IsActive);
}

/// <summary>Links cut at their limit. <see cref="IsTruncated"/> says that more exist (D-010).</summary>
public sealed record LinkConsentList(IReadOnlyList<LinkConsentView> Links, bool IsTruncated);
