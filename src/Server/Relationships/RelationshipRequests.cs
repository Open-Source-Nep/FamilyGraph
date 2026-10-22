using System.ComponentModel.DataAnnotations;
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
