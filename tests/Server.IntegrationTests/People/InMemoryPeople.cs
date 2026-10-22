using FamilyGraph.Application.People;
using FamilyGraph.Application.Persistence;
using FamilyGraph.Application.Relationships;
using FamilyGraph.Business.Audit;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace Server.IntegrationTests.People;

/// <summary>
/// People and link stores, queries, audit log and unit of work over in-memory lists. Like the
/// database, nothing added is visible until <see cref="SaveChangesAsync"/> runs.
/// </summary>
public sealed class InMemoryPeople
    : IPersonStore, IPersonQueries, IParentChildLinkStore, IRelationshipQueries, IAuditLog, IUnitOfWork
{
    private readonly List<Person> _added = [];
    private readonly List<ParentChildLink> _addedLinks = [];
    private readonly List<(AuditEvent Event, DateTimeOffset OccurredAt)> _addedEvents = [];

    public Dictionary<PersonId, Person> People { get; } = [];

    public Dictionary<LinkId, ParentChildLink> Links { get; } = [];

    public List<(AuditEvent Event, DateTimeOffset OccurredAt)> AuditEvents { get; } = [];

    /// <summary>Makes every ancestor snapshot incomplete, as if the bounded search hit its limit.</summary>
    public bool AncestorSearchHitsLimit { get; set; }

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

    public Task<IReadOnlySet<PersonId>> ParentsOfAsync(PersonId childId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<PersonId>>(
            NotDeletedLinks().Where(link => link.ChildId == childId).Select(link => link.ParentId).ToHashSet());

    public Task<AncestorSnapshot> AncestorsOfAsync(PersonId personId, CancellationToken cancellationToken)
    {
        var ancestors = new HashSet<PersonId>();
        var generation = new HashSet<PersonId> { personId };
        while (generation.Count > 0)
        {
            generation = NotDeletedLinks()
                .Where(link => generation.Contains(link.ChildId) && ancestors.Add(link.ParentId))
                .Select(link => link.ParentId)
                .ToHashSet();
        }

        return Task.FromResult(new AncestorSnapshot(personId, ancestors, isComplete: !AncestorSearchHitsLimit));
    }

    public void Add(ParentChildLink link) => _addedLinks.Add(link);

    public Task<DirectRelatives> ListDirectRelativesAsync(PersonId personId, int limit, CancellationToken cancellationToken)
    {
        var active = Links.Values.Where(link => link.IsActive).ToList();
        var parents = active
            .Where(link => link.ChildId == personId)
            .Select(link => new Relative(link.ParentId, People[link.ParentId].Name, link.Type))
            .OrderBy(parent => parent.Name.FirstName, StringComparer.Ordinal)
            .ToList();
        var children = active
            .Where(link => link.ParentId == personId)
            .Select(link => new ChildRelative(link.ChildId, People[link.ChildId].Name, link.Type, link.BirthOrder))
            .OrderBy(child => child.BirthOrder ?? int.MaxValue)
            .ThenBy(child => child.Name.FirstName, StringComparer.Ordinal)
            .ToList();
        return Task.FromResult(new DirectRelatives(
            parents.Take(limit).ToList(), parents.Count > limit, children.Take(limit).ToList(), children.Count > limit));
    }

    public void Add(IEnumerable<AuditEvent> auditEvents, DateTimeOffset occurredAt) =>
        _addedEvents.AddRange(auditEvents.Select(auditEvent => (auditEvent, occurredAt)));

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        foreach (var person in _added)
        {
            People[person.Id] = person;
        }

        foreach (var link in _addedLinks)
        {
            Links[link.Id] = link;
        }

        AuditEvents.AddRange(_addedEvents);
        _added.Clear();
        _addedLinks.Clear();
        _addedEvents.Clear();
        SaveCount++;
        return Task.CompletedTask;
    }

    private IEnumerable<ParentChildLink> NotDeletedLinks() => Links.Values.Where(link => !link.IsDeleted);
}
