namespace FamilyGraph.Business.People;

/// <summary>
/// A person's gender (PRD 01, D-030). It is optional: a profile without one has no gender, which
/// is not a value of its own. Relationship wording uses it where it is known and visible
/// (mother, father, son, daughter), and stays parent or child otherwise.
/// </summary>
public sealed class Gender : BaseEnum<Gender>
{
    public static readonly Gender Male = new(1, nameof(Male));

    public static readonly Gender Female = new(2, nameof(Female));

    public static readonly Gender Other = new(3, nameof(Other));

    private Gender(int value, string name)
        : base(value, name)
    {
    }
}
