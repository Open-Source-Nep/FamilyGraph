namespace FamilyGraph.Business.Claims;

public readonly record struct ClaimId
{
    public ClaimId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A claim ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static ClaimId New() => new(Guid.CreateVersion7());
}
