using FamilyGraph.Business;

namespace FamilyGraph.Application.Privacy;

/// <summary>What a viewer sees in place of a hidden profile (PRD 05). It carries no name or details.</summary>
public sealed class PersonPlaceholder : BaseEnum<PersonPlaceholder>
{
    public static readonly PersonPlaceholder Private = new(1, nameof(Private));

    /// <summary>A deleted profile, whose name is erased. Nobody sees through it.</summary>
    public static readonly PersonPlaceholder Deleted = new(2, nameof(Deleted));

    private PersonPlaceholder(int value, string name)
        : base(value, name)
    {
    }
}
