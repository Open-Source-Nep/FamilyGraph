using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Privacy;

/// <summary>
/// A person as the viewer may see them in a graph read (D-010). Only
/// <see cref="PrivacyProjection"/> creates one, so a read path cannot skip it.
/// </summary>
public sealed record PersonReference
{
    internal PersonReference(PersonId id, PersonName name, bool maintainedByViewer)
    {
        Id = id;
        Name = name;
        MaintainedByViewer = maintainedByViewer;
    }

    public PersonId Id { get; }

    public PersonName Name { get; }

    /// <summary>Whether the viewer maintains the profile, never which account does (D-019).</summary>
    public bool MaintainedByViewer { get; }
}
