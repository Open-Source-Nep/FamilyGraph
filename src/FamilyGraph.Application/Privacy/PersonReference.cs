using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Privacy;

/// <summary>
/// A person as the viewer may see them in any read (D-010). Only <see cref="PrivacyProjection"/>
/// creates one, so a read path cannot skip it. A hidden person keeps their ID, so links through
/// them stay traversable, but has a <see cref="Placeholder"/> and no <see cref="Name"/> (PRD 05).
/// </summary>
public sealed record PersonReference
{
    internal PersonReference(
        PersonId id, PersonName? name, PersonPlaceholder? placeholder, ProfileVisibility visibility, bool maintainedByViewer, ProfileImageId? imageId)
    {
        Id = id;
        Name = name;
        Placeholder = placeholder;
        Visibility = visibility;
        MaintainedByViewer = maintainedByViewer;
        ImageId = imageId;
    }

    public PersonId Id { get; }

    /// <summary><see langword="null"/> exactly when <see cref="Placeholder"/> is set.</summary>
    public PersonName? Name { get; }

    public PersonPlaceholder? Placeholder { get; }

    /// <summary>
    /// The profile's setting. A <c>Private</c> placeholder already says it is Private, and a
    /// <c>Deleted</c> one has no details to protect, so this reveals nothing more.
    /// </summary>
    public ProfileVisibility Visibility { get; }

    /// <summary>Whether the viewer maintains the profile, never which account does (D-019).</summary>
    public bool MaintainedByViewer { get; }

    /// <summary>The profile's image, or <see langword="null"/> if it has none or is a placeholder (D-028).</summary>
    public ProfileImageId? ImageId { get; }
}
