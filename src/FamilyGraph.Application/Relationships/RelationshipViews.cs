using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Application.Relationships;

/// <summary>
/// A newly created link as its creator sees it. <see cref="IsActive"/> is false while the other
/// endpoint's maintainer has not approved it (PRD 03).
/// </summary>
public sealed record CreatedLink(LinkId Id, PersonId ParentId, PersonId ChildId, ParentChildType Type, bool IsActive);

/// <summary>
/// A parent or child through an active link, with the link's ID and type (PRD 02). The type shows
/// next to a placeholder too (AC-05.9).
/// </summary>
public sealed record Relative(LinkId LinkId, PersonReference Person, ParentChildType Type);

/// <summary>A child also carries its birth-order number, or <see langword="null"/> while unordered (D-016).</summary>
public sealed record ChildRelative(LinkId LinkId, PersonReference Person, ParentChildType Type, int? BirthOrder);

/// <summary>A person's parents and children through active links. Each list says whether it was cut at its limit (D-010).</summary>
public sealed record DirectRelatives(
    IReadOnlyList<Relative> Parents,
    bool ParentsTruncated,
    IReadOnlyList<ChildRelative> Children,
    bool ChildrenTruncated);

/// <summary>One side of a link in the consent view: the person and its approval.</summary>
public sealed record LinkEndpointView(PersonReference Person, EndpointApproval Approval);

/// <summary>
/// A link's consent state as a maintainer of one of its endpoints sees it (D-021). PRD 07 lets
/// the current maintainers of both profiles see the link's history, so both approvals are shown.
/// </summary>
public sealed record LinkConsentView(
    LinkId Id, ParentChildType Type, LinkEndpointView Parent, LinkEndpointView Child, bool IsActive)
{
    internal static LinkConsentView From(LinkConsentData link, Viewer viewer) => new(
        link.Id,
        link.Type,
        new LinkEndpointView(PrivacyProjection.Project(link.Parent.Person, viewer), link.Parent.Approval),
        new LinkEndpointView(PrivacyProjection.Project(link.Child.Person, viewer), link.Child.Approval),
        link.IsActive);

    internal static LinkConsentView From(ParentChildLink link, Person parent, Person child, Viewer viewer) => From(
        new LinkConsentData(
            link.Id,
            link.Type,
            new LinkEndpointData(PersonRecord.From(parent, viewer.AccountId), link.ParentApproval),
            new LinkEndpointData(PersonRecord.From(child, viewer.AccountId), link.ChildApproval),
            link.IsActive),
        viewer);
}

/// <summary>Links cut at their limit. <see cref="IsTruncated"/> says that more exist (D-010).</summary>
public sealed record LinkConsentList(IReadOnlyList<LinkConsentView> Links, bool IsTruncated);
