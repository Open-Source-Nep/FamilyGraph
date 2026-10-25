using FamilyGraph.Application.People;
using FamilyGraph.Application.Privacy;

namespace Server.People;

/// <summary>
/// A profile as the signed-in user sees it. It never names the maintaining account. For a profile
/// hidden from you, <see cref="Placeholder"/> is <c>Private</c>, or <c>Deleted</c> for a deleted
/// one, and the name parts are null (PRD 05).
/// <see cref="Visibility"/> is <c>Public</c> or <c>Private</c>. <see cref="ImageId"/> names the
/// current image at <c>/api/people/{id}/image</c>, and is null without one or for a placeholder (D-028).
/// <see cref="Gender"/> is <c>Male</c>, <c>Female</c> or <c>Other</c>, and null when not set or for a placeholder (D-030).
/// <see cref="Description"/> and <see cref="BirthDate"/> are null when not set or for a placeholder, and the birth
/// date also when it is not shown to you (D-037).
/// </summary>
public sealed record PersonResponse(
    Guid Id,
    string? FirstName,
    string? MiddleName,
    string? LastName,
    string? Placeholder,
    string Visibility,
    bool MaintainedByYou,
    Guid? ImageId,
    string? Gender,
    string? Description,
    BirthDateResponse? BirthDate,
    bool BirthDateShown)
{
    internal static PersonResponse From(PersonReference person) => new(
        person.Id.Value,
        person.Name?.FirstName,
        person.Name?.MiddleName,
        person.Name?.LastName,
        person.Placeholder?.Name,
        person.Visibility.Name,
        person.MaintainedByViewer,
        person.ImageId?.Value,
        person.Gender?.Name,
        person.Description,
        BirthDateResponse.From(person.BirthDate),
        person.BirthDateShown);
}

/// <summary>A birth date as entered (D-037): <c>Bs</c> or <c>Ad</c>, a year, and the month and day if known.</summary>
public sealed record BirthDateResponse(string Calendar, int Year, int? Month, int? Day)
{
    internal static BirthDateResponse? From(FamilyGraph.Business.People.BirthDate? date) =>
        date is null ? null : new(date.Calendar.Name, date.Year, date.Month, date.Day);
}

/// <summary>
/// One of your profiles, which you always see by name, Private or not (PRD 05), with its image
/// and description to tell it apart (D-038).
/// </summary>
public sealed record PersonSummaryResponse(
    Guid Id,
    string FirstName,
    string? MiddleName,
    string? LastName,
    string Visibility,
    string? Gender,
    Guid? ImageId,
    string? Description);

/// <summary><see cref="IsTruncated"/> says that the list stopped at its limit (D-010).</summary>
public sealed record ProfileListResponse(IReadOnlyList<PersonSummaryResponse> People, bool IsTruncated)
{
    internal static ProfileListResponse From(ProfileList list) => new(
        [.. list.People.Select(person => new PersonSummaryResponse(
            person.Id.Value,
            person.Name.FirstName,
            person.Name.MiddleName,
            person.Name.LastName,
            person.Visibility.Name,
            person.Gender?.Name,
            person.ImageId?.Value,
            person.Description))],
        list.IsTruncated);
}

/// <summary>People found by name (D-032); <see cref="IsTruncated"/> says there are more, so refine the search (D-010).</summary>
public sealed record PersonSearchResponse(IReadOnlyList<PersonResponse> People, bool IsTruncated)
{
    internal static PersonSearchResponse From(PersonSearchResult result) =>
        new([.. result.People.Select(PersonResponse.From)], result.IsTruncated);
}

/// <summary>The ID of the image just stored (D-028).</summary>
public sealed record ImageResponse(Guid ImageId);
