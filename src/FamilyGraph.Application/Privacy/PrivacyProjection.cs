namespace FamilyGraph.Application.Privacy;

/// <summary>
/// The one component that decides what a viewer sees of a person in a graph read (D-010, D-023).
/// Every profile is Public until #18 adds privacy settings and profile deletion; it then turns
/// hidden profiles into <c>Private</c> or <c>Deleted</c> placeholders here, which stay
/// traversable (PRD 05).
/// </summary>
public static class PrivacyProjection
{
    public static PersonReference Project(PersonRecord person)
    {
        ArgumentNullException.ThrowIfNull(person);

        return new PersonReference(person.Id, person.Name, person.MaintainedByViewer);
    }
}
