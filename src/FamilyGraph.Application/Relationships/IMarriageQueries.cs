using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Application.Relationships;

/// <summary>
/// Read models of marriages (D-002, D-031), like <see cref="IRelationshipQueries"/>: spouse lists
/// hold only active marriages, the consent view the inactive marriages of the viewer's own
/// profiles. Lists sorted by name put people hidden from the viewer last, by ID (D-025).
/// </summary>
public interface IMarriageQueries
{
    /// <summary>
    /// The person's spouses through active marriages, current ones first, then by name. At most
    /// <paramref name="limit"/> of them (D-010).
    /// </summary>
    Task<SpouseListData> ListSpousesAsync(PersonId personId, Viewer viewer, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// The non-deleted, inactive marriages with at least one non-deleted spouse the viewer
    /// maintains, those waiting for the viewer's approval first, then by the spouses' names. At
    /// most <paramref name="limit"/> marriages (D-010).
    /// </summary>
    Task<MarriageConsentListData> ListInactiveMarriagesAsync(Viewer viewer, int limit, CancellationToken cancellationToken);
}

/// <summary>A spouse before privacy is applied.</summary>
public sealed record SpouseData(LinkId MarriageId, PersonRecord Person, bool IsFormer);

public sealed record SpouseListData(IReadOnlyList<SpouseData> Spouses, bool IsTruncated);

public sealed record MarriageConsentData(
    LinkId Id, LinkEndpointData First, LinkEndpointData Second, bool IsFormer, bool IsActive);

public sealed record MarriageConsentListData(IReadOnlyList<MarriageConsentData> Marriages, bool IsTruncated);
