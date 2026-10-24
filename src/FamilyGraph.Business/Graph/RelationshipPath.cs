using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Business.Graph;

/// <summary>
/// One step of a path: the link or marriage it follows, and the person it reaches as the previous
/// person's parent, child or spouse.
/// </summary>
public sealed record PathStep(GraphEdge Link, StepRelation Relation, PersonId PersonId);

/// <summary>
/// A path between two people through active links and marriages, one step per link (PRD 08). No person appears
/// twice. A path from a person to themselves has no steps.
/// </summary>
public sealed record RelationshipPath(PersonId Start, IReadOnlyList<PathStep> Steps)
{
    public PersonId End => Steps.Count == 0 ? Start : Steps[^1].PersonId;

    /// <summary>Whether a step crosses an adoptive link, which an explanation flags (PRD 08, AC-08.3).</summary>
    public bool CrossesAdoption => Steps.Any(step => step.Link is GraphLink { Type: var type } && type == ParentChildType.Adoptive);

    /// <summary>The people on the path from <see cref="Start"/> to <see cref="End"/>.</summary>
    public IEnumerable<PersonId> People => Steps.Select(step => step.PersonId).Prepend(Start);
}
