using FamilyGraph.Application.Privacy;
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

/// <summary>Search matches before privacy is applied, cut at their limit (D-010, D-032).</summary>
public sealed record PersonMatches(IReadOnlyList<PersonRecord> People, bool IsTruncated);

/// <summary>Search results as the viewer may see them. <see cref="IsTruncated"/> says to refine the search (D-010).</summary>
public sealed record PersonSearchResult(IReadOnlyList<PersonReference> People, bool IsTruncated);
