using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Business.Graph;

/// <summary>
/// Whose children a Bansawali follows further down (PRD 08, D-043). <see cref="Sons"/> is the
/// traditional line: only a biological son whose gender is known and visible to the viewer as
/// male continues it. <see cref="AllChildren"/> follows every child.
/// </summary>
public sealed class BansawaliLine : BaseEnum<BansawaliLine>
{
    public static readonly BansawaliLine Sons = new(1, nameof(Sons));

    public static readonly BansawaliLine AllChildren = new(2, nameof(AllChildren));

    private BansawaliLine(int value, string name)
        : base(value, name)
    {
    }

    /// <summary>
    /// Whether a child reached through a link of <paramref name="type"/> continues the line.
    /// <paramref name="visibleGender"/> is the gender the viewer may see: null when not set or
    /// hidden by a placeholder, so a hidden gender never shows in the line's shape.
    /// </summary>
    public bool Continues(ParentChildType type, Gender? visibleGender)
    {
        ArgumentNullException.ThrowIfNull(type);

        return this == AllChildren || (type == ParentChildType.Biological && visibleGender == Gender.Male);
    }
}
