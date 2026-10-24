using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Business.Graph;

/// <summary>
/// An active direct relationship as graph exploration sees it (PRD 08): a parent–child link or a
/// marriage. Only active ones take part in traversal (PRD 03).
/// </summary>
public abstract record GraphEdge(LinkId Id);

/// <summary>An active parent–child link, which keeps its biological or adoptive type (PRD 02).</summary>
public sealed record GraphLink(LinkId Id, PersonId ParentId, PersonId ChildId, ParentChildType Type) : GraphEdge(Id);

/// <summary>An active marriage, current or former (D-031). Its spouses are in the same generation.</summary>
public sealed record GraphMarriage(LinkId Id, PersonId FirstSpouseId, PersonId SecondSpouseId, bool IsFormer) : GraphEdge(Id);
