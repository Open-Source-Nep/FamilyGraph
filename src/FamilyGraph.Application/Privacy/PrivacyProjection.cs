using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Privacy;

/// <summary>
/// The one component that decides what a viewer sees of a person, on every read path (D-010,
/// D-025). A deleted profile is a <c>Deleted</c> placeholder for everyone, and a Private profile
/// a <c>Private</c> placeholder for everyone but its maintainer and administrators (PRD 05). A placeholder
/// has no name, gender or image (D-028, D-030). The placeholder keeps the person's ID, so active links through it
/// stay traversable, and privacy never changes which links are returned.
/// </summary>
public static class PrivacyProjection
{
    public static PersonReference Project(PersonRecord person, Viewer viewer)
    {
        ArgumentNullException.ThrowIfNull(person);
        ArgumentNullException.ThrowIfNull(viewer);

        var placeholder = PlaceholderFor(person, viewer);
        return placeholder is null
            ? new PersonReference(person.Id, person.Name, null, person.Visibility, person.MaintainedByViewer, person.ImageId, person.Gender)
            : new PersonReference(person.Id, null, placeholder, person.Visibility, maintainedByViewer: false, imageId: null, gender: null);
    }

    private static PersonPlaceholder? PlaceholderFor(PersonRecord person, Viewer viewer) =>
        person.IsDeleted ? PersonPlaceholder.Deleted
        : IsHidden(person, viewer) ? PersonPlaceholder.Private
        : null;

    private static bool IsHidden(PersonRecord person, Viewer viewer) =>
        person.Visibility == ProfileVisibility.Private && !person.MaintainedByViewer && !viewer.IsAdministrator;
}
