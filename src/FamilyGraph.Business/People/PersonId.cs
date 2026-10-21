namespace FamilyGraph.Business.People;

public readonly record struct PersonId
{
    public PersonId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A person ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static PersonId New() => new(Guid.CreateVersion7());
}
