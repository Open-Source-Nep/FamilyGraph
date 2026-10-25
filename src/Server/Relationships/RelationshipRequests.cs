using System.ComponentModel.DataAnnotations;
using FamilyGraph.Application.Relationships;
using FamilyGraph.Business.Relationships;

namespace Server.Relationships;

/// <summary>A new parent–child link. <see cref="Type"/> is <c>Biological</c> or <c>Adoptive</c> (400 otherwise).</summary>
public sealed class CreateLinkRequest
{
    [Required]
    public Guid? ParentId { get; init; }

    [Required]
    public Guid? ChildId { get; init; }

    [Required]
    [AllowedValues(nameof(ParentChildType.Biological), nameof(ParentChildType.Adoptive))]
    public string? Type { get; init; }

    internal ParentChildType ToType() => ParentChildType.FromName(Type!);
}

/// <summary>Which endpoint of the link to approve or disagree for: <c>Parent</c> or <c>Child</c> (400 otherwise).</summary>
public sealed class LinkConsentRequest
{
    [Required]
    [AllowedValues(nameof(LinkEndpoint.Parent), nameof(LinkEndpoint.Child))]
    public string? Endpoint { get; init; }

    internal LinkEndpoint ToEndpoint() => LinkEndpoint.FromName(Endpoint!);
}

/// <summary>A parent's children in birth order, eldest first, by their link IDs (D-016, D-037).</summary>
public sealed class ChildrenOrderRequest
{
    [Required]
    public IReadOnlyList<Guid>? LinkIds { get; init; }
}
