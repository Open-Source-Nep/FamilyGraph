using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Application.Relationships;

/// <summary>
/// A newly created marriage as its creator sees it. <see cref="IsActive"/> is false while the other
/// spouse's maintainer has not approved it (PRD 03).
/// </summary>
public sealed record CreatedMarriage(LinkId Id, PersonId FirstSpouseId, PersonId SecondSpouseId, bool IsActive);

/// <summary>A spouse through an active marriage, current or former (D-031).</summary>
public sealed record Spouse(LinkId MarriageId, PersonReference Person, bool IsFormer);

/// <summary>A person's spouses. <see cref="IsTruncated"/> says the list was cut at its limit (D-010).</summary>
public sealed record SpouseList(IReadOnlyList<Spouse> Spouses, bool IsTruncated);

/// <summary>
/// A marriage's consent state as a maintainer of one of its spouses sees it, with both approvals
/// (PRD 07). The spouses are in ID order.
/// </summary>
public sealed record MarriageConsentView(
    LinkId Id, LinkEndpointView First, LinkEndpointView Second, bool IsFormer, bool IsActive)
{
    internal static MarriageConsentView From(MarriageConsentData marriage, Viewer viewer) => new(
        marriage.Id,
        new LinkEndpointView(PrivacyProjection.Project(marriage.First.Person, viewer), marriage.First.Approval),
        new LinkEndpointView(PrivacyProjection.Project(marriage.Second.Person, viewer), marriage.Second.Approval),
        marriage.IsFormer,
        marriage.IsActive);

    internal static MarriageConsentView From(Marriage marriage, Person first, Person second, Viewer viewer) => From(
        new MarriageConsentData(
            marriage.Id,
            new LinkEndpointData(PersonRecord.From(first, viewer.AccountId), marriage.FirstApproval),
            new LinkEndpointData(PersonRecord.From(second, viewer.AccountId), marriage.SecondApproval),
            marriage.IsFormer,
            marriage.IsActive),
        viewer);
}

/// <summary>Marriages cut at their limit. <see cref="IsTruncated"/> says that more exist (D-010).</summary>
public sealed record MarriageConsentList(IReadOnlyList<MarriageConsentView> Marriages, bool IsTruncated);
