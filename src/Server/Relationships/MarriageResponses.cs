using FamilyGraph.Application.Relationships;

namespace Server.Relationships;

/// <summary>
/// The spouses are in ID order. <see cref="IsActive"/> is false while the other spouse's maintainer
/// has not approved the marriage.
/// </summary>
public sealed record CreatedMarriageResponse(Guid Id, Guid FirstSpouseId, Guid SecondSpouseId, bool IsActive)
{
    internal static CreatedMarriageResponse From(CreatedMarriage marriage) =>
        new(marriage.Id.Value, marriage.FirstSpouseId.Value, marriage.SecondSpouseId.Value, marriage.IsActive);
}

/// <summary>
/// A spouse through an active marriage; <see cref="IsFormer"/> when the marriage ended (D-031). A
/// spouse hidden from you has <see cref="Placeholder"/> <c>Private</c> or <c>Deleted</c> and no
/// name or gender (PRD 05).
/// </summary>
public sealed record SpouseResponse(
    Guid MarriageId,
    Guid Id,
    string? FirstName,
    string? MiddleName,
    string? LastName,
    string? Placeholder,
    string Visibility,
    string? Gender,
    bool MaintainedByYou,
    bool IsFormer);

/// <summary>A person's spouses, current ones first. <see cref="IsTruncated"/> says the list stopped at its limit (D-010).</summary>
public sealed record SpouseListResponse(IReadOnlyList<SpouseResponse> Spouses, bool IsTruncated)
{
    internal static SpouseListResponse From(SpouseList list) => new(
        [.. list.Spouses.Select(spouse => new SpouseResponse(
            spouse.MarriageId.Value,
            spouse.Person.Id.Value,
            spouse.Person.Name?.FirstName,
            spouse.Person.Name?.MiddleName,
            spouse.Person.Name?.LastName,
            spouse.Person.Placeholder?.Name,
            spouse.Person.Visibility.Name,
            spouse.Person.Gender?.Name,
            spouse.Person.MaintainedByViewer,
            spouse.IsFormer))],
        list.IsTruncated);
}

/// <summary>A marriage's consent on both sides, shown to the maintainers of its spouses (D-031).</summary>
public sealed record MarriageConsentResponse(
    Guid Id, LinkEndpointResponse First, LinkEndpointResponse Second, bool IsFormer, bool IsActive)
{
    internal static MarriageConsentResponse From(MarriageConsentView marriage) => new(
        marriage.Id.Value,
        LinkEndpointResponse.From(marriage.First),
        LinkEndpointResponse.From(marriage.Second),
        marriage.IsFormer,
        marriage.IsActive);
}

/// <summary>The inactive marriages of your profiles. <see cref="IsTruncated"/> says that more exist (D-010).</summary>
public sealed record MarriageConsentListResponse(IReadOnlyList<MarriageConsentResponse> Marriages, bool IsTruncated)
{
    internal static MarriageConsentListResponse From(MarriageConsentList list) =>
        new([.. list.Marriages.Select(MarriageConsentResponse.From)], list.IsTruncated);
}
