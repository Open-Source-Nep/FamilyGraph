namespace FamilyGraph.Business.People;

/// <summary>Opaque reference to a user account. The domain never sees identity framework types (D-008).</summary>
public readonly record struct AccountId
{
    public AccountId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("An account ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }
}
