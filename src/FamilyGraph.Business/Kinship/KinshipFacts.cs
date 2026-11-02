using FamilyGraph.Business.Graph;
using FamilyGraph.Business.People;

namespace FamilyGraph.Business.Kinship;

/// <summary>A child's place among a parent's children by birth order: 1 is the eldest.</summary>
public sealed record ChildRank(int Number, bool Youngest);

/// <summary>
/// What a label rule may test about one step of a path (D-048): the relation, the visible gender of
/// the person reached, for a child step after a parent step their age relative to the person before,
/// and for a child step their rank among the parent's children, overall and among those of their
/// gender. Null means hidden or unknown.
/// </summary>
public sealed record KinshipStepFacts(
    StepRelation Relation,
    Gender? Gender,
    RelativeAge? Age = null,
    ChildRank? Rank = null,
    ChildRank? SameGenderRank = null);

/// <summary>
/// What label rules may test about a path (D-048): the visible gender of the start person and the
/// facts of each step. Built only from what the viewer may see, so a label leaks nothing (PRD 05).
/// </summary>
public sealed record KinshipFacts(Gender? StartGender, IReadOnlyList<KinshipStepFacts> Steps)
{
    /// <summary>
    /// The facts of <paramref name="path"/>. <paramref name="genderOf"/> gives each person's gender
    /// as the viewer sees it (null when hidden or not set). <paramref name="links"/> are active
    /// links that include every child link of each parent a child step leaves; the parents shared
    /// by two siblings decide their relative age, and a parent's children their rank. Birth order
    /// is the only source: an elder or younger sibling is known when every shared parent who
    /// ordered both agrees and at least one did; a rank is known when all the parent's children
    /// have distinct birth orders and, among the same gender, every child's gender is known.
    /// </summary>
    public static KinshipFacts Of(
        RelationshipPath path,
        Func<PersonId, Gender?> genderOf,
        IReadOnlyCollection<GraphLink> links,
        IReadOnlySet<PersonId> completeParents)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(genderOf);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(completeParents);

        var people = path.People.ToList();
        var steps = new List<KinshipStepFacts>(path.Steps.Count);
        for (var index = 0; index < path.Steps.Count; index++)
        {
            var step = path.Steps[index];
            var gender = genderOf(step.PersonId);
            if (step.Relation != StepRelation.Child)
            {
                steps.Add(new KinshipStepFacts(step.Relation, gender));
                continue;
            }

            var parentId = people[index];
            var age = index > 0 && path.Steps[index - 1].Relation == StepRelation.Parent
                ? AgeOf(step.PersonId, people[index - 1], links)
                : null;
            if (!completeParents.Contains(parentId))
            {
                steps.Add(new KinshipStepFacts(step.Relation, gender, age));
                continue;
            }

            var children = links.Where(link => link.ParentId == parentId).DistinctBy(link => link.ChildId).ToList();
            var rank = RankOf(step.PersonId, children);
            var sameGenderRank = gender is not null && children.All(link => genderOf(link.ChildId) is not null)
                ? RankOf(step.PersonId, [.. children.Where(link => genderOf(link.ChildId) == gender)])
                : null;
            steps.Add(new KinshipStepFacts(step.Relation, gender, age, rank, sameGenderRank));
        }
        return new KinshipFacts(genderOf(path.Start), steps);
    }

    /// <summary>Whether <paramref name="siblingId"/> is elder or younger than <paramref name="personId"/>.</summary>
    private static RelativeAge? AgeOf(PersonId siblingId, PersonId personId, IReadOnlyCollection<GraphLink> links)
    {
        RelativeAge? age = null;
        foreach (var parentId in links.Where(link => link.ChildId == personId).Select(link => link.ParentId).Distinct())
        {
            var mine = links.FirstOrDefault(link => link.ParentId == parentId && link.ChildId == personId)?.BirthOrder;
            var theirs = links.FirstOrDefault(link => link.ParentId == parentId && link.ChildId == siblingId)?.BirthOrder;
            if (mine is not { } own || theirs is not { } other || own == other)
            {
                continue;
            }

            var here = other < own ? RelativeAge.Elder : RelativeAge.Younger;
            if (age is not null && age != here)
            {
                return null;
            }
            age = here;
        }
        return age;
    }

    private static ChildRank? RankOf(PersonId childId, IReadOnlyList<GraphLink> children)
    {
        if (children.Any(link => link.BirthOrder is null)
            || children.Select(link => link.BirthOrder).Distinct().Count() != children.Count
            || children.FirstOrDefault(link => link.ChildId == childId) is not { BirthOrder: { } order })
        {
            return null;
        }
        var number = children.Count(link => link.BirthOrder < order) + 1;
        return new ChildRank(number, number == children.Count);
    }
}
