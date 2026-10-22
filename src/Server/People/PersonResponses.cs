using FamilyGraph.Application.People;

namespace Server.People;

/// <summary>A profile as the signed-in user sees it. It never names the maintaining account.</summary>
public sealed record PersonResponse(Guid Id, string FirstName, string? MiddleName, string? LastName, bool MaintainedByYou)
{
    internal static PersonResponse From(PersonView person) => new(
        person.Id.Value, person.Name.FirstName, person.Name.MiddleName, person.Name.LastName, person.MaintainedByViewer);
}

public sealed record PersonSummaryResponse(Guid Id, string FirstName, string? MiddleName, string? LastName);

/// <summary><see cref="IsTruncated"/> says that the list stopped at its limit (D-010).</summary>
public sealed record ProfileListResponse(IReadOnlyList<PersonSummaryResponse> People, bool IsTruncated)
{
    internal static ProfileListResponse From(ProfileList list) => new(
        [.. list.People.Select(person => new PersonSummaryResponse(
            person.Id.Value, person.Name.FirstName, person.Name.MiddleName, person.Name.LastName))],
        list.IsTruncated);
}
