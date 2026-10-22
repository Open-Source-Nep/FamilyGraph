using FamilyGraph.Business.People;

namespace FamilyGraph.Application.People;

/// <summary>
/// A person profile as the viewing account sees it. It says whether the viewer maintains the
/// profile but never which account does.
/// </summary>
public sealed record PersonView(PersonId Id, PersonName Name, bool MaintainedByViewer);

public sealed record PersonSummary(PersonId Id, PersonName Name);

/// <summary>A list cut at its limit. <see cref="IsTruncated"/> says that more exist (D-010).</summary>
public sealed record ProfileList(IReadOnlyList<PersonSummary> People, bool IsTruncated);
