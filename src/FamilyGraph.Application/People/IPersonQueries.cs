using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.People;

namespace FamilyGraph.Application.People;

/// <summary>Read models of person profiles (D-002). Every list takes a limit (D-010).</summary>
public interface IPersonQueries
{
    /// <summary>The person before privacy is applied, for <see cref="PrivacyProjection"/>.</summary>
    Task<PersonRecord?> FindAsync(PersonId id, AccountId viewerId, CancellationToken cancellationToken);

    /// <summary>An image by ID, for a caller that has already checked the viewer may see it.</summary>
    Task<ImageContent?> FindImageAsync(ProfileImageId id, CancellationToken cancellationToken);

    /// <summary>
    /// The non-deleted profiles the account maintains, sorted by name, at most
    /// <paramref name="limit"/> of them.
    /// </summary>
    Task<ProfileList> ListMaintainedByAsync(AccountId maintainerId, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// The non-deleted profiles whose first, middle or last name contains every one of
    /// <paramref name="terms"/> (ignoring case), among the Public ones and those the viewer
    /// maintains, sorted by name, at most <paramref name="limit"/> of them (D-032). A Private profile
    /// someone else maintains is never matched, so its hidden name cannot be searched for (PRD 05).
    /// </summary>
    Task<PersonMatches> SearchAsync(
        IReadOnlyList<string> terms, AccountId viewerId, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// One page of the non-deleted profiles whose first, middle or last name or description contains
    /// every one of <paramref name="terms"/> (ignoring case; none matches everyone), sorted by name
    /// and ID, skipping <paramref name="offset"/> and returning at most <paramref name="limit"/>
    /// (D-042). Only Public profiles and those the viewer maintains are listed, plus every Private
    /// one when <paramref name="includePrivate"/> (administrators, PRD 05), so a hidden profile is
    /// never listed or matched by its hidden name. <see cref="PersonMatches.IsTruncated"/> says more follow.
    /// </summary>
    Task<PersonMatches> BrowseAsync(
        IReadOnlyList<string> terms, AccountId viewerId, bool includePrivate, int offset, int limit, CancellationToken cancellationToken);
}
