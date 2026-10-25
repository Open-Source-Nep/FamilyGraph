using FamilyGraph.Application.People;
using FamilyGraph.Application.Persistence;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Application.Relationships;
using FamilyGraph.Business.Audit;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace Server.IntegrationTests.People;

/// <summary>
/// People, link and marriage stores, queries, audit log and unit of work over in-memory lists. Like the
/// database, nothing added is visible until <see cref="SaveChangesAsync"/> runs.
/// </summary>
public sealed class InMemoryPeople
    : IPersonStore,
        IPersonQueries,
        IProfileImageStore,
        IParentChildLinkStore,
        IRelationshipQueries,
        IMarriageStore,
        IMarriageQueries,
        IAuditLog,
        IUnitOfWork
{
    private readonly List<Person> _added = [];
    private readonly List<ParentChildLink> _addedLinks = [];
    private readonly List<Marriage> _addedMarriages = [];
    private readonly List<ProfileImage> _addedImages = [];
    private readonly List<ProfileImage> _removedImages = [];
    private readonly List<(AuditEvent Event, DateTimeOffset OccurredAt)> _addedEvents = [];

    public Dictionary<PersonId, Person> People { get; } = [];

    public Dictionary<LinkId, ParentChildLink> Links { get; } = [];

    public Dictionary<LinkId, Marriage> Marriages { get; } = [];

    public Dictionary<ProfileImageId, ProfileImage> Images { get; } = [];

    public List<(AuditEvent Event, DateTimeOffset OccurredAt)> AuditEvents { get; } = [];

    /// <summary>Makes every ancestor snapshot incomplete, as if the bounded search hit its limit.</summary>
    public bool AncestorSearchHitsLimit { get; set; }

    public int SaveCount { get; private set; }

    public Task<Person?> FindAsync(PersonId id, CancellationToken cancellationToken) =>
        Task.FromResult(People.GetValueOrDefault(id));

    public Task<IReadOnlyList<Person>> ListMaintainedByAsync(AccountId maintainerId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Person>>(
            [.. People.Values.Where(person => person.IsMaintainedBy(maintainerId) && !person.IsDeleted)]);

    public void Add(Person person) => _added.Add(person);

    public Task<PersonRecord?> FindAsync(PersonId id, AccountId viewerId, CancellationToken cancellationToken) =>
        Task.FromResult(People.TryGetValue(id, out var person) ? PersonRecord.From(person, viewerId) : null);

    public Task<ImageContent?> FindImageAsync(ProfileImageId id, CancellationToken cancellationToken) =>
        Task.FromResult(Images.TryGetValue(id, out var image) ? new ImageContent(image.Id, image.Format, image.Content) : null);

    public Task<ProfileImage?> FindAsync(ProfileImageId id, CancellationToken cancellationToken) =>
        Task.FromResult(Images.GetValueOrDefault(id));

    public void Add(ProfileImage image) => _addedImages.Add(image);

    public void Remove(ProfileImage image) => _removedImages.Add(image);

    public Task<ProfileList> ListMaintainedByAsync(AccountId maintainerId, int limit, CancellationToken cancellationToken)
    {
        var maintained = People.Values
            .Where(person => person.IsMaintainedBy(maintainerId) && !person.IsDeleted)
            .OrderBy(person => person.Name.FirstName, StringComparer.Ordinal)
            .Select(person => new PersonSummary(person.Id, person.Name, person.Visibility, person.Gender, person.ImageId, person.Description))
            .ToList();
        return Task.FromResult(new ProfileList(maintained.Take(limit).ToList(), maintained.Count > limit));
    }

    public Task<PersonMatches> SearchAsync(
        IReadOnlyList<string> terms, AccountId viewerId, int limit, CancellationToken cancellationToken)
    {
        var matches = People.Values
            .Where(person => !person.IsDeleted
                && (person.Visibility == ProfileVisibility.Public || person.IsMaintainedBy(viewerId))
                && terms.All(term => new[] { person.Name.FirstName, person.Name.MiddleName, person.Name.LastName }
                    .Any(part => part?.Contains(term, StringComparison.OrdinalIgnoreCase) == true)))
            .OrderBy(person => person.Name.FirstName, StringComparer.Ordinal)
            .ThenBy(person => person.Name.MiddleName, StringComparer.Ordinal)
            .ThenBy(person => person.Name.LastName, StringComparer.Ordinal)
            .ThenBy(person => person.Id.Value)
            .Select(person => PersonRecord.From(person, viewerId))
            .ToList();
        return Task.FromResult(new PersonMatches(matches.Take(limit).ToList(), matches.Count > limit));
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

    /// <summary>A birth date shown to others on a profile the viewer may see, which orders children (D-039).</summary>
    private static DateOnly? ShownBirth(PersonRecord person, Viewer viewer) =>
        !IsHidden(person, viewer) && person.BirthDateShown ? person.BirthDate?.SortDate : null;

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
            .ThenBy(child => ShownBirth(child.Person, viewer) ?? DateOnly.MaxValue)
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

    Task<Marriage?> IMarriageStore.FindAsync(LinkId id, CancellationToken cancellationToken) =>
        Task.FromResult(Marriages.GetValueOrDefault(id));

    public Task<bool> ExistsAsync(PersonId spouseId, PersonId otherSpouseId, CancellationToken cancellationToken) =>
        Task.FromResult(Marriages.Values.Any(marriage => !marriage.IsDeleted
            && ((marriage.FirstSpouseId == spouseId && marriage.SecondSpouseId == otherSpouseId)
                || (marriage.FirstSpouseId == otherSpouseId && marriage.SecondSpouseId == spouseId))));

    public Task<bool> AreParentAndChildAsync(PersonId personId, PersonId otherPersonId, CancellationToken cancellationToken) =>
        Task.FromResult(NotDeletedLinks().Any(link =>
            (link.ParentId == personId && link.ChildId == otherPersonId)
            || (link.ParentId == otherPersonId && link.ChildId == personId)));

    public void Add(Marriage marriage) => _addedMarriages.Add(marriage);

    /// <summary>Current marriages first, then hidden people last, by ID, like the database query.</summary>
    public Task<SpouseListData> ListSpousesAsync(PersonId personId, Viewer viewer, int limit, CancellationToken cancellationToken)
    {
        var spouses = Marriages.Values
            .Where(marriage => marriage.IsActive && (marriage.FirstSpouseId == personId || marriage.SecondSpouseId == personId))
            .Select(marriage => new SpouseData(marriage.Id, Record(marriage.OtherSpouseOf(personId), viewer), marriage.IsFormer))
            .OrderBy(spouse => spouse.IsFormer)
            .ThenBy(spouse => IsHidden(spouse.Person, viewer))
            .ThenBy(spouse => IsHidden(spouse.Person, viewer) ? "" : spouse.Person.Name.FirstName, StringComparer.Ordinal)
            .ThenBy(spouse => spouse.Person.Id.Value)
            .ToList();
        return Task.FromResult(new SpouseListData(spouses.Take(limit).ToList(), spouses.Count > limit));
    }

    public Task<MarriageConsentListData> ListInactiveMarriagesAsync(Viewer viewer, int limit, CancellationToken cancellationToken)
    {
        var viewerId = viewer.AccountId;
        var inactive = Marriages.Values
            .Where(marriage => !marriage.IsDeleted && !marriage.IsActive)
            .Select(marriage => (Marriage: marriage, First: People[marriage.FirstSpouseId], Second: People[marriage.SecondSpouseId]))
            .Where(row => Owns(row.First, viewerId) || Owns(row.Second, viewerId))
            .OrderByDescending(row =>
                (Owns(row.First, viewerId) && row.Marriage.FirstApproval == EndpointApproval.Pending)
                || (Owns(row.Second, viewerId) && row.Marriage.SecondApproval == EndpointApproval.Pending))
            .ThenBy(row => row.Marriage.Id.Value)
            .Select(row => new MarriageConsentData(
                row.Marriage.Id,
                new LinkEndpointData(Record(row.First.Id, viewer), row.Marriage.FirstApproval),
                new LinkEndpointData(Record(row.Second.Id, viewer), row.Marriage.SecondApproval),
                row.Marriage.IsFormer,
                IsActive: false))
            .ToList();
        return Task.FromResult(new MarriageConsentListData(inactive.Take(limit).ToList(), inactive.Count > limit));
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

        foreach (var marriage in _addedMarriages)
        {
            Marriages[marriage.Id] = marriage;
        }

        foreach (var image in _addedImages)
        {
            Images[image.Id] = image;
        }

        foreach (var image in _removedImages)
        {
            Images.Remove(image.Id);
        }

        AuditEvents.AddRange(_addedEvents);
        _addedImages.Clear();
        _removedImages.Clear();
        _added.Clear();
        _addedLinks.Clear();
        _addedMarriages.Clear();
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
