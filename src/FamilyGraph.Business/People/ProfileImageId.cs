namespace FamilyGraph.Business.People;

/// <summary>Identifies one uploaded image. A new image gets a new ID, so the ID also names the version.</summary>
public readonly record struct ProfileImageId
{
    public ProfileImageId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A profile image ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static ProfileImageId New() => new(Guid.CreateVersion7());
}
