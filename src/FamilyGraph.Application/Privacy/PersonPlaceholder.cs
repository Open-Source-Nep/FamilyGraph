using FamilyGraph.Business;

namespace FamilyGraph.Application.Privacy;

/// <summary>What a viewer sees in place of a hidden profile (PRD 05). It carries no name or details.</summary>
public sealed class PersonPlaceholder : BaseEnum<PersonPlaceholder>
{
    public static readonly PersonPlaceholder Private = new(1, nameof(Private));

    private PersonPlaceholder(int value, string name)
        : base(value, name)
    {
    }
}
