using FamilyGraph.Application.Relationships;

namespace Server.Relationships;

/// <summary><see cref="IsActive"/> is false while the other profile's maintainer has not approved the link.</summary>
public sealed record CreatedLinkResponse(Guid Id, Guid ParentId, Guid ChildId, string Type, bool IsActive)
{
    internal static CreatedLinkResponse From(CreatedLink link) =>
        new(link.Id.Value, link.ParentId.Value, link.ChildId.Value, link.Type.Name, link.IsActive);
}

public sealed record RelativeResponse(Guid Id, string FirstName, string? MiddleName, string? LastName, string Type);

/// <summary><see cref="BirthOrder"/> is null while the child is unordered (D-016).</summary>
public sealed record ChildResponse(
    Guid Id, string FirstName, string? MiddleName, string? LastName, string Type, int? BirthOrder);

/// <summary>Parents and children through active links. Each list says whether it stopped at its limit (D-010).</summary>
public sealed record DirectRelativesResponse(
    IReadOnlyList<RelativeResponse> Parents,
    bool ParentsTruncated,
    IReadOnlyList<ChildResponse> Children,
    bool ChildrenTruncated)
{
    internal static DirectRelativesResponse From(DirectRelatives relatives) => new(
        [.. relatives.Parents.Select(parent => new RelativeResponse(
            parent.Id.Value, parent.Name.FirstName, parent.Name.MiddleName, parent.Name.LastName, parent.Type.Name))],
        relatives.ParentsTruncated,
        [.. relatives.Children.Select(child => new ChildResponse(
            child.Id.Value, child.Name.FirstName, child.Name.MiddleName, child.Name.LastName, child.Type.Name, child.BirthOrder))],
        relatives.ChildrenTruncated);
}
