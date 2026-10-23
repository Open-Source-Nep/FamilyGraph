using FamilyGraph.Application.People;
using FamilyGraph.Application.Privacy;

namespace Server.People;

/// <summary>
/// A profile as the signed-in user sees it. It never names the maintaining account. For a profile
/// hidden from you, <see cref="Placeholder"/> is <c>Private</c> and the name parts are null (PRD 05).
/// <see cref="Visibility"/> is <c>Public</c> or <c>Private</c>.
/// </summary>
public sealed record PersonResponse(
    Guid Id,
    string? FirstName,
    string? MiddleName,
    string? LastName,
    string? Placeholder,
    string Visibility,
    bool MaintainedByYou)
{
    internal static PersonResponse From(PersonReference person) => new(
        person.Id.Value,
        person.Name?.FirstName,
        person.Name?.MiddleName,
        person.Name?.LastName,
        person.Placeholder?.Name,
        person.Visibility.Name,
        person.MaintainedByViewer);
}

/// <summary>One of your profiles, which you always see by name, Private or not (PRD 05).</summary>
public sealed record PersonSummaryResponse(Guid Id, string FirstName, string? MiddleName, string? LastName, string Visibility);

/// <summary><see cref="IsTruncated"/> says that the list stopped at its limit (D-010).</summary>
public sealed record ProfileListResponse(IReadOnlyList<PersonSummaryResponse> People, bool IsTruncated)
{
    internal static ProfileListResponse From(ProfileList list) => new(
        [.. list.People.Select(person => new PersonSummaryResponse(
            person.Id.Value, person.Name.FirstName, person.Name.MiddleName, person.Name.LastName, person.Visibility.Name))],
        list.IsTruncated);
}
