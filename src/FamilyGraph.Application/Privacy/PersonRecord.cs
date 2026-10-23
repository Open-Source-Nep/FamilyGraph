using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Privacy;

/// <summary>
/// A person as a query service loads it, before privacy is applied. Only
/// <see cref="PrivacyProjection"/> turns it into a <see cref="PersonReference"/> that a use case
/// may return (D-010).
/// </summary>
public sealed record PersonRecord(PersonId Id, PersonName Name, ProfileVisibility Visibility, bool MaintainedByViewer)
{
    public static PersonRecord From(Person person, AccountId viewerId)
    {
        ArgumentNullException.ThrowIfNull(person);

        return new PersonRecord(person.Id, person.Name, person.Visibility, person.IsMaintainedBy(viewerId));
    }
}
