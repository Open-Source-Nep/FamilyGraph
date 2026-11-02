namespace FamilyGraph.Business.Kinship;

/// <summary>
/// Whether a sibling reached on a path is older or younger than the person the path came from, by
/// the birth order their shared parents set (PRD 02, D-016, D-048). Birth dates never decide it.
/// </summary>
public sealed class RelativeAge : BaseEnum<RelativeAge>
{
    public static readonly RelativeAge Elder = new(1, nameof(Elder));

    public static readonly RelativeAge Younger = new(2, nameof(Younger));

    private RelativeAge(int value, string name)
        : base(value, name)
    {
    }
}
