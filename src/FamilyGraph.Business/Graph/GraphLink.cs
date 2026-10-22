using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Business.Graph;

/// <summary>
/// An active parent–child link as graph exploration sees it (PRD 08). Only active links take part
/// in traversal (PRD 03), and each keeps its biological or adoptive type (PRD 02).
/// </summary>
public sealed record GraphLink(LinkId Id, PersonId ParentId, PersonId ChildId, ParentChildType Type);
