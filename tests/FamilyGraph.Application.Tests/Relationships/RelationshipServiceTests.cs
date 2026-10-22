using FamilyGraph.Application.People;
using FamilyGraph.Application.Relationships;
using FamilyGraph.Application.Tests.People;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Application.Tests.Relationships;

public class RelationshipServiceTests
{
    private static readonly AccountId Alice = new(Guid.NewGuid());
    private static readonly AccountId Bob = new(Guid.NewGuid());
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 30, 0, TimeSpan.Zero);

    private readonly InMemoryPeople _store = new();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private RelationshipService Service(int directRelativesLimit = RelationshipLimits.DefaultDirectRelativesLimit) =>
        new(_store, _store, _store, _store, _store, _store, new FixedTimeProvider(Now), new RelationshipLimits(directRelativesLimit));

    [Theory]
    [Trait("AC", "AC-02.1")]
    [InlineData(nameof(ParentChildType.Biological))]
    [InlineData(nameof(ParentChildType.Adoptive))]
    public async Task Create_saves_the_link_with_its_type_and_creation_event(string typeName)
    {
        var type = ParentChildType.FromName(typeName);
        var parent = AddPerson("Hari", Alice);
        var child = AddPerson("Maya", Alice);

        var created = await Service().CreateLinkAsync(parent, child, type, Alice, Cancellation);

        var link = _store.Links[created.Id];
        Assert.Equal((parent, child, type), (link.ParentId, link.ChildId, link.Type));
        Assert.True(created.IsActive);
        var (auditEvent, occurredAt) = Assert.Single(_store.AuditEvents);
        Assert.Equal(
            new LinkCreated(link.Id, parent, child, type, EndpointApproval.Approved, EndpointApproval.Approved, Alice),
            auditEvent);
        Assert.Equal(Now, occurredAt);
        Assert.Equal(1, _store.SaveCount);
    }

    [Fact]
    public async Task Endpoint_maintained_by_someone_else_starts_pending()
    {
        var parent = AddPerson("Hari", Bob);
        var child = AddPerson("Maya", Alice);

        var created = await Service().CreateLinkAsync(parent, child, ParentChildType.Biological, Alice, Cancellation);

        var link = _store.Links[created.Id];
        Assert.Equal(EndpointApproval.Pending, link.ParentApproval);
        Assert.Equal(EndpointApproval.Approved, link.ChildApproval);
        Assert.False(created.IsActive);
    }

    [Fact]
    public async Task Duplicate_counts_a_pending_link()
    {
        var parent = AddPerson("Hari", Bob);
        var child = AddPerson("Maya", Alice);
        var service = Service();
        await service.CreateLinkAsync(parent, child, ParentChildType.Biological, Alice, Cancellation);

        await Assert.ThrowsAsync<DuplicateLinkException>(
            () => service.CreateLinkAsync(parent, child, ParentChildType.Adoptive, Alice, Cancellation));

        Assert.Single(_store.Links);
        Assert.Equal(1, _store.SaveCount);
    }

    [Fact]
    [Trait("AC", "AC-02.6")]
    public async Task Cycle_through_a_pending_link_is_rejected()
    {
        var grandparent = AddPerson("Ram", Bob);
        var parent = AddPerson("Hari", Alice);
        var child = AddPerson("Maya", Alice);
        var service = Service();
        await service.CreateLinkAsync(grandparent, parent, ParentChildType.Biological, Alice, Cancellation);
        await service.CreateLinkAsync(parent, child, ParentChildType.Biological, Alice, Cancellation);

        await Assert.ThrowsAsync<LinkCycleDetectedException>(
            () => service.CreateLinkAsync(child, grandparent, ParentChildType.Biological, Alice, Cancellation));
    }

    [Fact]
    [Trait("AC", "AC-02.7")]
    public async Task Link_is_rejected_when_the_ancestor_search_hits_its_limit()
    {
        var parent = AddPerson("Hari", Alice);
        var child = AddPerson("Maya", Alice);
        _store.AncestorSearchHitsLimit = true;

        await Assert.ThrowsAsync<LinkCycleUnverifiableException>(
            () => Service().CreateLinkAsync(parent, child, ParentChildType.Biological, Alice, Cancellation));

        Assert.Empty(_store.Links);
    }

    [Fact]
    public async Task Someone_who_maintains_neither_endpoint_is_refused_and_nothing_is_saved()
    {
        var parent = AddPerson("Hari", Alice);
        var child = AddPerson("Maya", Alice);

        await Assert.ThrowsAsync<NotLinkEndpointMaintainerException>(
            () => Service().CreateLinkAsync(parent, child, ParentChildType.Biological, Bob, Cancellation));

        Assert.Empty(_store.Links);
        Assert.Empty(_store.AuditEvents);
    }

    [Fact]
    public async Task Link_to_an_unknown_person_is_not_found()
    {
        var child = AddPerson("Maya", Alice);

        await Assert.ThrowsAsync<PersonNotFoundException>(
            () => Service().CreateLinkAsync(PersonId.New(), child, ParentChildType.Biological, Alice, Cancellation));
    }

    [Fact]
    [Trait("AC", "AC-02.5")]
    public async Task Relatives_list_only_active_links_and_respect_the_limit()
    {
        var child = AddPerson("Maya", Alice);
        var sita = AddPerson("Sita", Alice);
        var hari = AddPerson("Hari", Alice);
        var pending = AddPerson("Gita", Bob);
        var service = Service(directRelativesLimit: 1);
        await service.CreateLinkAsync(sita, child, ParentChildType.Biological, Alice, Cancellation);
        await service.CreateLinkAsync(hari, child, ParentChildType.Adoptive, Alice, Cancellation);
        await service.CreateLinkAsync(pending, child, ParentChildType.Biological, Alice, Cancellation);

        var relatives = await service.ListDirectRelativesAsync(child, Bob, Cancellation);

        var parent = Assert.Single(relatives.Parents);
        Assert.Equal((hari, ParentChildType.Adoptive), (parent.Id, parent.Type));
        Assert.True(relatives.ParentsTruncated);
        Assert.Empty(relatives.Children);
        Assert.False(relatives.ChildrenTruncated);
    }

    [Fact]
    public async Task Relatives_of_an_unknown_person_are_not_found()
    {
        await Assert.ThrowsAsync<PersonNotFoundException>(
            () => Service().ListDirectRelativesAsync(PersonId.New(), Alice, Cancellation));
    }

    private PersonId AddPerson(string firstName, AccountId maintainer)
    {
        var person = Person.Create(new PersonName(firstName), maintainer);
        person.TakeEvents();
        _store.People[person.Id] = person;
        return person.Id;
    }
}
