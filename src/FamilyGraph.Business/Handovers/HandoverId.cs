namespace FamilyGraph.Business.Handovers;

public readonly record struct HandoverId
{
    public HandoverId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A handover ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static HandoverId New() => new(Guid.CreateVersion7());
}
