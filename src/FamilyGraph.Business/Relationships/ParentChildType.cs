namespace FamilyGraph.Business.Relationships;

/// <summary>The only directly registered relationship types (PRD 02). Adoption is link metadata.</summary>
public sealed class ParentChildType : BaseEnum<ParentChildType>
{
    public static readonly ParentChildType Biological = new(1, nameof(Biological));

    public static readonly ParentChildType Adoptive = new(2, nameof(Adoptive));

    private ParentChildType(int value, string name)
        : base(value, name)
    {
    }
}
