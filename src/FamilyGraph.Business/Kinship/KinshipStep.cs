using FamilyGraph.Business.Graph;
using FamilyGraph.Business.People;

namespace FamilyGraph.Business.Kinship;

/// <summary>
/// One step of a label rule (D-048): the relation it follows, and optionally the gender of the
/// person it reaches, for a child step right after a parent step whether that sibling is
/// <see cref="Age"/> elder or younger than the person before, and for any child step their
/// <see cref="Position"/> among the parent's children.
/// </summary>
public sealed record KinshipStep
{
    public KinshipStep(StepRelation relation, Gender? gender = null, RelativeAge? age = null, ChildPosition? position = null)
    {
        ArgumentNullException.ThrowIfNull(relation);
        if ((age is not null || position is not null) && relation != StepRelation.Child)
        {
            throw new ArgumentException("Only a child step can ask for an age or a position.");
        }

        Relation = relation;
        Gender = gender;
        Age = age;
        Position = position;
    }

    public StepRelation Relation { get; }

    public Gender? Gender { get; }

    public RelativeAge? Age { get; }

    public ChildPosition? Position { get; }

    /// <summary>How many conditions beyond the relation this step has; more is more specific.</summary>
    internal int Conditions => (Gender is null ? 0 : 1) + (Age is null ? 0 : 1) + (Position is null ? 0 : 1);

    internal bool Matches(KinshipStepFacts facts) =>
        facts.Relation == Relation
        && (Gender is null || facts.Gender == Gender)
        && (Age is null || facts.Age == Age)
        && (Position is null || Position.Matches(facts));
}
