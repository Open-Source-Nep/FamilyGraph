using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Privacy;

/// <summary>
/// The one component that decides what a viewer sees of a person, on every read path (D-010,
/// D-025). A Private profile becomes a <c>Private</c> placeholder for everyone but its maintainer
/// and administrators (PRD 05). The placeholder keeps the person's ID, so active links through it
/// stay traversable, and privacy never changes which links are returned.
/// </summary>
public static class PrivacyProjection
{
    public static PersonReference Project(PersonRecord person, Viewer viewer)
    {
        ArgumentNullException.ThrowIfNull(person);
        ArgumentNullException.ThrowIfNull(viewer);

        return IsHidden(person, viewer)
            ? new PersonReference(person.Id, null, PersonPlaceholder.Private, person.Visibility, maintainedByViewer: false)
            : new PersonReference(person.Id, person.Name, null, person.Visibility, person.MaintainedByViewer);
    }

    private static bool IsHidden(PersonRecord person, Viewer viewer) =>
        person.Visibility == ProfileVisibility.Private && !person.MaintainedByViewer && !viewer.IsAdministrator;
}
