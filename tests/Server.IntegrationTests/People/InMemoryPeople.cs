using FamilyGraph.Application.People;
using FamilyGraph.Application.Persistence;
using FamilyGraph.Application.Privacy;
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

    public Task<PersonRecord?> FindAsync(PersonId id, AccountId viewerId, CancellationToken cancellationToken) =>
        Task.FromResult(People.TryGetValue(id, out var person) ? PersonRecord.From(person, viewerId) : null);

    public Task<ProfileList> ListMaintainedByAsync(AccountId maintainerId, int limit, CancellationToken cancellationToken)
    {
        var maintained = People.Values
            .Where(person => person.IsMaintainedBy(maintainerId) && !person.IsDeleted)
            .OrderBy(person => person.Name.FirstName, StringComparer.Ordinal)
            .Select(person => new PersonSummary(person.Id, person.Name, person.Visibility))
            .ToList();
        return Task.FromResult(new ProfileList(maintained.Take(limit).ToList(), maintained.Count > limit));
    }

    /// <summary>Returns the stored link itself, so changes to it are visible before saving.</summary>
    public Task<ParentChildLink?> FindAsync(LinkId id, CancellationToken cancellationToken) =>
        Task.FromResult(Links.GetValueOrDefault(id));

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

    /// <summary>Sorts people hidden from the viewer last, by ID, like the database query.</summary>
    public Task<DirectRelativesData> ListDirectRelativesAsync(
        PersonId personId, Viewer viewer, int limit, CancellationToken cancellationToken)
    {
        var active = Links.Values.Where(link => link.IsActive).ToList();
        var parents = active
            .Where(link => link.ChildId == personId)
            .Select(link => new RelativeData(link.Id, Record(link.ParentId, viewer), link.Type, null))
            .OrderBy(parent => IsHidden(parent.Person, viewer))
            .ThenBy(parent => IsHidden(parent.Person, viewer) ? "" : parent.Person.Name.FirstName, StringComparer.Ordinal)
            .ThenBy(parent => parent.Person.Id.Value)
            .ToList();
        var children = active
            .Where(link => link.ParentId == personId)
            .Select(link => new RelativeData(link.Id, Record(link.ChildId, viewer), link.Type, link.BirthOrder))
            .OrderBy(child => child.BirthOrder ?? int.MaxValue)
            .ThenBy(child => IsHidden(child.Person, viewer))
            .ThenBy(child => IsHidden(child.Person, viewer) ? "" : child.Person.Name.FirstName, StringComparer.Ordinal)
            .ThenBy(child => child.Person.Id.Value)
            .ToList();
        return Task.FromResult(new DirectRelativesData(
            parents.Take(limit).ToList(), parents.Count > limit, children.Take(limit).ToList(), children.Count > limit));
    }

    public Task<LinkConsentListData> ListInactiveLinksAsync(Viewer viewer, int limit, CancellationToken cancellationToken)
    {
        var viewerId = viewer.AccountId;
        var inactive = NotDeletedLinks()
            .Where(link => !link.IsActive)
            .Select(link => (Link: link, Parent: People[link.ParentId], Child: People[link.ChildId]))
            .Where(row => Owns(row.Parent, viewerId) || Owns(row.Child, viewerId))
            .OrderByDescending(row =>
                (Owns(row.Parent, viewerId) && row.Link.ParentApproval == EndpointApproval.Pending)
                || (Owns(row.Child, viewerId) && row.Link.ChildApproval == EndpointApproval.Pending))
            .ThenBy(row => IsHidden(Record(row.Parent.Id, viewer), viewer))
            .ThenBy(row => IsHidden(Record(row.Parent.Id, viewer), viewer) ? "" : row.Parent.Name.FirstName, StringComparer.Ordinal)
            .ThenBy(row => IsHidden(Record(row.Child.Id, viewer), viewer))
            .ThenBy(row => IsHidden(Record(row.Child.Id, viewer), viewer) ? "" : row.Child.Name.FirstName, StringComparer.Ordinal)
            .ThenBy(row => row.Link.Id.Value)
            .Select(row => new LinkConsentData(
                row.Link.Id,
                row.Link.Type,
                new LinkEndpointData(Record(row.Parent.Id, viewer), row.Link.ParentApproval),
                new LinkEndpointData(Record(row.Child.Id, viewer), row.Link.ChildApproval),
                IsActive: false))
            .ToList();
        return Task.FromResult(new LinkConsentListData(inactive.Take(limit).ToList(), inactive.Count > limit));
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

    private static bool Owns(Person person, AccountId viewerId) => person.IsMaintainedBy(viewerId) && !person.IsDeleted;

    private static bool IsHidden(PersonRecord person, Viewer viewer) =>
        person.IsDeleted
        || (person.Visibility == ProfileVisibility.Private && !person.MaintainedByViewer && !viewer.IsAdministrator);

    private PersonRecord Record(PersonId id, Viewer viewer) => PersonRecord.From(People[id], viewer.AccountId);

    private IEnumerable<ParentChildLink> NotDeletedLinks() => Links.Values.Where(link => !link.IsDeleted);
}
