namespace FamilyGraph.Business.People;

/// <summary>Who deleted a profile: its maintainer, or an administrator for moderation (AC-05.11).</summary>
public sealed class ProfileDeleter : BaseEnum<ProfileDeleter>
{
    public static readonly ProfileDeleter Maintainer = new(1, nameof(Maintainer));

    public static readonly ProfileDeleter Administrator = new(2, nameof(Administrator));

    private ProfileDeleter(int value, string name)
        : base(value, name)
    {
    }
}
