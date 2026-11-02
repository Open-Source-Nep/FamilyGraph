using FamilyGraph.Business.Audit;
using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Import;

/// <summary>
/// What an import needs beyond the people, link, marriage and handover stores (D-033, D-049). Those rows
/// are added through their stores and saved by <see cref="Persistence.IUnitOfWork"/> inside the transaction
/// <see cref="BeginAsync"/> opens; accounts, images and audit rows are saved here as they are added, so an
/// import of any size never holds them all in memory.
/// </summary>
public interface IImportStore
{
    /// <summary>
    /// Opens the import's transaction. Until it ends, no other request can add or change a profile,
    /// link, marriage, image or handover, so the instance stays empty between the check and the save.
    /// </summary>
    Task<IImportTransaction> BeginAsync(CancellationToken cancellationToken);

    /// <summary>Whether the instance has any profile, deleted or not.</summary>
    Task<bool> HasPeopleAsync(CancellationToken cancellationToken);

    /// <summary>The accounts here with these Google IDs, by Google ID.</summary>
    Task<IReadOnlyDictionary<string, AccountId>> FindAccountsByGoogleIdAsync(
        IReadOnlyCollection<string> googleIds, CancellationToken cancellationToken);

    /// <summary>Which of these account IDs already exist here.</summary>
    Task<IReadOnlySet<AccountId>> FindExistingAccountsAsync(
        IReadOnlyCollection<AccountId> ids, CancellationToken cancellationToken);

    /// <summary>Creates an account with its ID, a user's role and, when it has one, its Google login.</summary>
    Task CreateAccountAsync(ImportedAccount account, CancellationToken cancellationToken);

    /// <summary>Saves one image and lets it go.</summary>
    Task AddImageAsync(ProfileImage image, CancellationToken cancellationToken);

    /// <summary>Saves audit history rows as they were, then lets them go.</summary>
    Task AddAuditEventsAsync(IReadOnlyList<ImportedAuditEvent> auditEvents, CancellationToken cancellationToken);
}

/// <summary>An account to create with its exported ID.</summary>
public sealed record ImportedAccount(AccountId Id, string? GoogleId, string Email, bool EmailVerified);

/// <summary>An audit history row to restore as it was, with account IDs already mapped to this instance.</summary>
public sealed record ImportedAuditEvent(
    Guid Id,
    DateTimeOffset OccurredAt,
    AuditFamily Family,
    string Type,
    AccountId ActorId,
    Guid SubjectId,
    string Data);

/// <summary>The import's transaction; disposing it without <see cref="CommitAsync"/> rolls it back.</summary>
public interface IImportTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
