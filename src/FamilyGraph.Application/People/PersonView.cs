using FamilyGraph.Business.People;

namespace FamilyGraph.Application.People;

/// <summary>
/// A profile on the list of profiles the viewer maintains. The maintainer always sees their own
/// profiles' names, Private or not (PRD 05), so the list needs no privacy projection. Deleted
/// profiles are not on it: there is nothing left to maintain (D-026).
/// </summary>
public sealed record PersonSummary(PersonId Id, PersonName Name, ProfileVisibility Visibility, Gender? Gender);

/// <summary>A list cut at its limit. <see cref="IsTruncated"/> says that more exist (D-010).</summary>
public sealed record ProfileList(IReadOnlyList<PersonSummary> People, bool IsTruncated);

/// <summary>An image's bytes and format, to serve as they are (D-028).</summary>
public sealed record ImageContent(ProfileImageId Id, ImageFormat Format, byte[] Content);
