namespace FamilyGraph.Business.Graph;

/// <summary>
/// How the person a path step reaches relates to the person before it: their parent, their child or
/// their spouse (PRD 08, D-031, "Sita → parent: Maya").
/// </summary>
public sealed class StepRelation : BaseEnum<StepRelation>
{
    public static readonly StepRelation Parent = new(1, nameof(Parent));

    public static readonly StepRelation Child = new(2, nameof(Child));

    public static readonly StepRelation Spouse = new(3, nameof(Spouse));

    private StepRelation(int value, string name)
        : base(value, name)
    {
    }
}
