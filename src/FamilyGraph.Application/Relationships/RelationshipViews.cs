using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Application.Relationships;

/// <summary>
/// A newly created link as its creator sees it. <see cref="IsActive"/> is false while the other
/// endpoint's maintainer has not approved it (PRD 03).
/// </summary>
public sealed record CreatedLink(LinkId Id, PersonId ParentId, PersonId ChildId, ParentChildType Type, bool IsActive);

/// <summary>A parent or child through an active link, with the link's type (PRD 02).</summary>
public sealed record Relative(PersonId Id, PersonName Name, ParentChildType Type);

/// <summary>A child also carries its birth-order number, or <see langword="null"/> while unordered (D-016).</summary>
public sealed record ChildRelative(PersonId Id, PersonName Name, ParentChildType Type, int? BirthOrder);

/// <summary>A person's parents and children through active links. Each list says whether it was cut at its limit (D-010).</summary>
public sealed record DirectRelatives(
    IReadOnlyList<Relative> Parents,
    bool ParentsTruncated,
    IReadOnlyList<ChildRelative> Children,
    bool ChildrenTruncated);
