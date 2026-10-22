using FamilyGraph.Application.People;
using FamilyGraph.Application.Persistence;
using FamilyGraph.Business.Audit;
using FamilyGraph.Business.People;

namespace Server.IntegrationTests.People;

/// <summary>
/// Stores, queries, audit log and unit of work over in-memory lists. Like the database, nothing
/// added is visible until <see cref="SaveChangesAsync"/> runs.
/// </summary>
public sealed class InMemoryPeople : IPersonStore, IPersonQueries, IAuditLog, IUnitOfWork
{
    private readonly List<Person> _added = [];
    private readonly List<(AuditEvent Event, DateTimeOffset OccurredAt)> _addedEvents = [];

    public Dictionary<PersonId, Person> People { get; } = [];

    public List<(AuditEvent Event, DateTimeOffset OccurredAt)> AuditEvents { get; } = [];

    public int SaveCount { get; private set; }

    public Task<Person?> FindAsync(PersonId id, CancellationToken cancellationToken) =>
        Task.FromResult(People.GetValueOrDefault(id));

    public void Add(Person person) => _added.Add(person);

    public Task<PersonView?> FindAsync(PersonId id, AccountId viewerId, CancellationToken cancellationToken) =>
        Task.FromResult(People.TryGetValue(id, out var person)
            ? new PersonView(person.Id, person.Name, person.IsMaintainedBy(viewerId))
            : null);

    public Task<ProfileList> ListMaintainedByAsync(AccountId maintainerId, int limit, CancellationToken cancellationToken)
    {
        var maintained = People.Values
            .Where(person => person.IsMaintainedBy(maintainerId))
            .OrderBy(person => person.Name.FirstName, StringComparer.Ordinal)
            .Select(person => new PersonSummary(person.Id, person.Name))
            .ToList();
        return Task.FromResult(new ProfileList(maintained.Take(limit).ToList(), maintained.Count > limit));
    }

    public void Add(IEnumerable<AuditEvent> auditEvents, DateTimeOffset occurredAt) =>
        _addedEvents.AddRange(auditEvents.Select(auditEvent => (auditEvent, occurredAt)));

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        foreach (var person in _added)
        {
            People[person.Id] = person;
        }

        AuditEvents.AddRange(_addedEvents);
        _added.Clear();
        _addedEvents.Clear();
        SaveCount++;
        return Task.CompletedTask;
    }
}
