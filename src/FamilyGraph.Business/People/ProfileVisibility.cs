namespace FamilyGraph.Business.People;

/// <summary>
/// Who sees a profile's name (PRD 05). A Public profile shows its name to signed-in users; a
/// Private one shows a <c>Private</c> placeholder to everyone but its maintainer and administrators.
/// </summary>
public sealed class ProfileVisibility : BaseEnum<ProfileVisibility>
{
    public static readonly ProfileVisibility Public = new(1, nameof(Public));

    public static readonly ProfileVisibility Private = new(2, nameof(Private));

    private ProfileVisibility(int value, string name)
        : base(value, name)
    {
    }
}
