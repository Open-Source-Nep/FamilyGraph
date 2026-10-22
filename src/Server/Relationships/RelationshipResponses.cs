using FamilyGraph.Application.Relationships;

namespace Server.Relationships;

/// <summary><see cref="IsActive"/> is false while the other profile's maintainer has not approved the link.</summary>
public sealed record CreatedLinkResponse(Guid Id, Guid ParentId, Guid ChildId, string Type, bool IsActive)
{
    internal static CreatedLinkResponse From(CreatedLink link) =>
        new(link.Id.Value, link.ParentId.Value, link.ChildId.Value, link.Type.Name, link.IsActive);
}

/// <summary>A relative through an active link. <see cref="MaintainedByYou"/> never names the maintaining account (D-019).</summary>
public sealed record RelativeResponse(
    Guid LinkId, Guid Id, string FirstName, string? MiddleName, string? LastName, string Type, bool MaintainedByYou);

/// <summary><see cref="BirthOrder"/> is null while the child is unordered (D-016).</summary>
public sealed record ChildResponse(
    Guid LinkId,
    Guid Id,
    string FirstName,
    string? MiddleName,
    string? LastName,
    string Type,
    bool MaintainedByYou,
    int? BirthOrder);

/// <summary>Parents and children through active links. Each list says whether it stopped at its limit (D-010).</summary>
public sealed record DirectRelativesResponse(
    IReadOnlyList<RelativeResponse> Parents,
    bool ParentsTruncated,
    IReadOnlyList<ChildResponse> Children,
    bool ChildrenTruncated)
{
    internal static DirectRelativesResponse From(DirectRelatives relatives) => new(
        [.. relatives.Parents.Select(parent => new RelativeResponse(
            parent.LinkId.Value,
            parent.Id.Value,
            parent.Name.FirstName,
            parent.Name.MiddleName,
            parent.Name.LastName,
            parent.Type.Name,
            parent.MaintainedByViewer))],
        relatives.ParentsTruncated,
        [.. relatives.Children.Select(child => new ChildResponse(
            child.LinkId.Value,
            child.Id.Value,
            child.Name.FirstName,
            child.Name.MiddleName,
            child.Name.LastName,
            child.Type.Name,
            child.MaintainedByViewer,
            child.BirthOrder))],
        relatives.ChildrenTruncated);
}

/// <summary>
/// One side of a link: the person, its approval (<c>Pending</c>, <c>Approved</c> or
/// <c>Disagreed</c>) and whether you maintain it (D-021).
/// </summary>
public sealed record LinkEndpointResponse(
    Guid Id, string FirstName, string? MiddleName, string? LastName, string Approval, bool MaintainedByYou)
{
    internal static LinkEndpointResponse From(LinkEndpointView endpoint) => new(
        endpoint.Id.Value,
        endpoint.Name.FirstName,
        endpoint.Name.MiddleName,
        endpoint.Name.LastName,
        endpoint.Approval.Name,
        endpoint.MaintainedByViewer);
}

/// <summary>A link's per-endpoint consent, shown to the maintainers of its profiles (D-021).</summary>
public sealed record LinkConsentResponse(
    Guid Id, string Type, LinkEndpointResponse Parent, LinkEndpointResponse Child, bool IsActive)
{
    internal static LinkConsentResponse From(LinkConsentView link) => new(
        link.Id.Value,
        link.Type.Name,
        LinkEndpointResponse.From(link.Parent),
        LinkEndpointResponse.From(link.Child),
        link.IsActive);
}

/// <summary>The inactive links of your profiles. <see cref="IsTruncated"/> says that more exist (D-010).</summary>
public sealed record LinkConsentListResponse(IReadOnlyList<LinkConsentResponse> Links, bool IsTruncated)
{
    internal static LinkConsentListResponse From(LinkConsentList list) =>
        new([.. list.Links.Select(LinkConsentResponse.From)], list.IsTruncated);
}
