using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Import;

/// <summary>
/// What an import needs beyond the people, link and marriage stores (D-033). The profiles, links and
/// marriages are added through those stores and saved by <see cref="Persistence.IUnitOfWork"/>
/// inside the transaction <see cref="BeginAsync"/> opens.
/// </summary>
public interface IImportStore
{
    /// <summary>
    /// Opens the import's transaction. Until it ends, no other request can add or change a profile,
    /// link or marriage, so the instance stays empty between the check and the save.
    /// </summary>
    Task<IImportTransaction> BeginAsync(CancellationToken cancellationToken);

    /// <summary>Whether the instance has any profile, deleted or not.</summary>
    Task<bool> HasPeopleAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The accounts for the given emails, ignoring case and surrounding spaces. An email is only in
    /// the result when exactly one account has it and it is verified.
    /// </summary>
    Task<IReadOnlyDictionary<string, AccountId>> FindAccountsByVerifiedEmailAsync(
        IReadOnlyCollection<string> emails, CancellationToken cancellationToken);
}

/// <summary>The import's transaction; disposing it without <see cref="CommitAsync"/> rolls it back.</summary>
public interface IImportTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
