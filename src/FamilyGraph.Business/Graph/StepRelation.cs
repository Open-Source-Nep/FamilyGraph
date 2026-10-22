namespace FamilyGraph.Business.Graph;

/// <summary>
/// How the person a path step reaches relates to the person before it: their parent or their child
/// (PRD 08, "Sita → parent: Maya").
/// </summary>
public sealed class StepRelation : BaseEnum<StepRelation>
{
    public static readonly StepRelation Parent = new(1, nameof(Parent));

    public static readonly StepRelation Child = new(2, nameof(Child));

    private StepRelation(int value, string name)
        : base(value, name)
    {
    }
}
