using FamilyGraph.Business.People;

namespace FamilyGraph.Application.People;

/// <summary>Loads and adds <see cref="Person"/> aggregates for changes (D-002). Reads go through <see cref="IPersonQueries"/>.</summary>
public interface IPersonStore
{
    Task<Person?> FindAsync(PersonId id, CancellationToken cancellationToken);

    /// <summary>The non-deleted profiles the account maintains, loaded for changes (D-035).</summary>
    Task<IReadOnlyList<Person>> ListMaintainedByAsync(AccountId maintainerId, CancellationToken cancellationToken);

    void Add(Person person);
}
