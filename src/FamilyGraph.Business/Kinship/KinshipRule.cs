using FamilyGraph.Business.Graph;
using FamilyGraph.Business.People;

namespace FamilyGraph.Business.Kinship;

/// <summary>
/// A label rule of a culture (PRD 08, D-048): an exact sequence of steps from the start person,
/// optionally the start person's gender, and the label for the person the path reaches.
/// </summary>
public sealed record KinshipRule
{
    public KinshipRule(IReadOnlyList<KinshipStep> steps, KinshipLabel label, Gender? startGender = null)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(label);
        if (steps.Count == 0)
        {
            throw new ArgumentException("A rule needs at least one step.", nameof(steps));
        }
        for (var index = 0; index < steps.Count; index++)
        {
            if (steps[index].Age is not null && (index == 0 || steps[index - 1].Relation != StepRelation.Parent))
            {
                throw new ArgumentException("Elder or younger applies only to a child step right after a parent step.", nameof(steps));
            }
        }

        Steps = steps;
        Label = label;
        StartGender = startGender;
    }

    public IReadOnlyList<KinshipStep> Steps { get; }

    public KinshipLabel Label { get; }

    public Gender? StartGender { get; }

    /// <summary>How many conditions beyond the relations the rule has; the most specific match wins.</summary>
    internal int Specificity => (StartGender is null ? 0 : 1) + Steps.Sum(step => step.Conditions);

    internal bool Matches(KinshipFacts facts) =>
        facts.Steps.Count == Steps.Count
        && (StartGender is null || facts.StartGender == StartGender)
        && Steps.Zip(facts.Steps).All(pair => pair.First.Matches(pair.Second));
}
