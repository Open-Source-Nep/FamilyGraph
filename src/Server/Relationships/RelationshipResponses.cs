using FamilyGraph.Application.Relationships;

namespace Server.Relationships;

/// <summary><see cref="IsActive"/> is false while the other profile's maintainer has not approved the link.</summary>
public sealed record CreatedLinkResponse(Guid Id, Guid ParentId, Guid ChildId, string Type, bool IsActive)
{
    internal static CreatedLinkResponse From(CreatedLink link) =>
        new(link.Id.Value, link.ParentId.Value, link.ChildId.Value, link.Type.Name, link.IsActive);
}

/// <summary>
/// A relative through an active link. <see cref="MaintainedByYou"/> never names the maintaining
/// account (D-019). A relative hidden from you has <see cref="Placeholder"/> <c>Private</c> or
/// <c>Deleted</c> and no name or gender; the link's type still shows (PRD 05, AC-05.9, D-030).
/// </summary>
public sealed record RelativeResponse(
    Guid LinkId,
    Guid Id,
    string? FirstName,
    string? MiddleName,
    string? LastName,
    string? Placeholder,
    string Visibility,
    string Type,
    bool MaintainedByYou,
    string? Gender);

/// <summary><see cref="BirthOrder"/> is null while the child is unordered (D-016).</summary>
public sealed record ChildResponse(
    Guid LinkId,
    Guid Id,
    string? FirstName,
    string? MiddleName,
    string? LastName,
    string? Placeholder,
    string Visibility,
    string Type,
    bool MaintainedByYou,
    int? BirthOrder,
    string? Gender);

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
            parent.Person.Id.Value,
            parent.Person.Name?.FirstName,
            parent.Person.Name?.MiddleName,
            parent.Person.Name?.LastName,
            parent.Person.Placeholder?.Name,
            parent.Person.Visibility.Name,
            parent.Type.Name,
            parent.Person.MaintainedByViewer,
            parent.Person.Gender?.Name))],
        relatives.ParentsTruncated,
        [.. relatives.Children.Select(child => new ChildResponse(
            child.LinkId.Value,
            child.Person.Id.Value,
            child.Person.Name?.FirstName,
            child.Person.Name?.MiddleName,
            child.Person.Name?.LastName,
            child.Person.Placeholder?.Name,
            child.Person.Visibility.Name,
            child.Type.Name,
            child.Person.MaintainedByViewer,
            child.BirthOrder,
            child.Person.Gender?.Name))],
        relatives.ChildrenTruncated);
}

/// <summary>
/// One side of a link: the person, its approval (<c>Pending</c>, <c>Approved</c> or
/// <c>Disagreed</c>) and whether you maintain it (D-021). A person hidden from you has
/// <see cref="Placeholder"/> <c>Private</c> or <c>Deleted</c> and no name (PRD 05).
/// </summary>
public sealed record LinkEndpointResponse(
    Guid Id,
    string? FirstName,
    string? MiddleName,
    string? LastName,
    string? Placeholder,
    string Visibility,
    string Approval,
    bool MaintainedByYou,
    string? Gender)
{
    internal static LinkEndpointResponse From(LinkEndpointView endpoint) => new(
        endpoint.Person.Id.Value,
        endpoint.Person.Name?.FirstName,
        endpoint.Person.Name?.MiddleName,
        endpoint.Person.Name?.LastName,
        endpoint.Person.Placeholder?.Name,
        endpoint.Person.Visibility.Name,
        endpoint.Approval.Name,
        endpoint.Person.MaintainedByViewer,
        endpoint.Person.Gender?.Name);
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
