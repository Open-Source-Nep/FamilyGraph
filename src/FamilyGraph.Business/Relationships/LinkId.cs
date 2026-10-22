namespace FamilyGraph.Business.Relationships;

public readonly record struct LinkId
{
    public LinkId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A link ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static LinkId New() => new(Guid.CreateVersion7());
}
