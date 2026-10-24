using FamilyGraph.Application.People;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Application.Relationships;
using FamilyGraph.Application.Tests.People;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Application.Tests.Relationships;

public class MarriageServiceTests
{
    private static readonly AccountId Alice = new(Guid.NewGuid());
    private static readonly AccountId Bob = new(Guid.NewGuid());
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 6, 0, 0, TimeSpan.Zero);

    private readonly InMemoryPeople _store = new();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static Viewer As(AccountId account) => new(account, IsAdministrator: false);

    private MarriageService Service(int limit = RelationshipLimits.DefaultDirectRelativesLimit) =>
        new(_store, _store, _store, _store, _store, _store, new FixedTimeProvider(Now), new RelationshipLimits(limit, limit));

    [Fact]
    [Trait("AC", "AC-02.11")]
    public async Task Create_saves_the_marriage_with_its_creation_event()
    {
        var ram = AddPerson("Ram", Alice);
        var sita = AddPerson("Sita", Bob);

        var created = await Service().CreateAsync(ram, sita, Alice, Cancellation);

        var marriage = _store.Marriages[created.Id];
        Assert.False(created.IsActive);
        Assert.Equal(EndpointApproval.Pending, marriage.ApprovalOf(sita));
        var (auditEvent, occurredAt) = Assert.Single(_store.AuditEvents);
        Assert.IsType<MarriageCreated>(auditEvent);
        Assert.Equal(Now, occurredAt);
    }

    [Fact]
    [Trait("AC", "AC-02.13")]
    public async Task A_second_marriage_of_the_same_pair_is_rejected_in_either_order()
    {
        var ram = AddPerson("Ram", Alice);
        var sita = AddPerson("Sita", Alice);
        await Service().CreateAsync(ram, sita, Alice, Cancellation);

        await Assert.ThrowsAsync<DuplicateMarriageException>(() => Service().CreateAsync(sita, ram, Alice, Cancellation));
    }

    [Fact]
    [Trait("AC", "AC-02.12")]
    public async Task The_other_maintainer_approves_and_the_marriage_leaves_the_inactive_list()
    {
        var ram = AddPerson("Ram", Alice);
        var sita = AddPerson("Sita", Bob);
        var created = await Service().CreateAsync(ram, sita, Alice, Cancellation);

        var waiting = Assert.Single((await Service().ListInactiveAsync(As(Bob), Cancellation)).Marriages);
        Assert.Equal(created.Id, waiting.Id);

        var approved = await Service().ApproveAsync(created.Id, sita, As(Bob), Cancellation);

        Assert.True(approved.IsActive);
        Assert.Empty((await Service().ListInactiveAsync(As(Bob), Cancellation)).Marriages);
    }

    [Fact]
    public async Task Consent_for_someone_outside_the_marriage_is_rejected()
    {
        var ram = AddPerson("Ram", Alice);
        var sita = AddPerson("Sita", Bob);
        var other = AddPerson("Gita", Bob);
        var created = await Service().CreateAsync(ram, sita, Alice, Cancellation);

        await Assert.ThrowsAsync<NotASpouseException>(() => Service().ApproveAsync(created.Id, other, As(Bob), Cancellation));
    }

    [Fact]
    [Trait("AC", "AC-02.12")]
    [Trait("AC", "AC-03.1")]
    public async Task Withdrawing_approval_removes_the_spouse_from_the_list()
    {
        var ram = AddPerson("Ram", Alice);
        var sita = AddPerson("Sita", Bob);
        var created = await Service().CreateAsync(ram, sita, Alice, Cancellation);
        await Service().ApproveAsync(created.Id, sita, As(Bob), Cancellation);

        var disagreed = await Service().DisagreeAsync(created.Id, ram, As(Alice), Cancellation);

        Assert.False(disagreed.IsActive);
        Assert.Empty((await Service().ListSpousesAsync(sita, As(Bob), Cancellation)).Spouses);
    }

    [Fact]
    [Trait("AC", "AC-02.14")]
    [Trait("AC", "AC-02.15")]
    public async Task A_former_marriage_stays_listed_after_the_current_ones()
    {
        var ram = AddPerson("Ram", Alice);
        var gita = AddPerson("Gita", Alice);
        var sita = AddPerson("Sita", Alice);
        var first = await Service().CreateAsync(ram, gita, Alice, Cancellation);
        await Service().CreateAsync(ram, sita, Alice, Cancellation);

        await Service().SetFormerAsync(first.Id, isFormer: true, Alice, Cancellation);

        var spouses = (await Service().ListSpousesAsync(ram, As(Bob), Cancellation)).Spouses;
        Assert.Equal([("Sita", false), ("Gita", true)], spouses.Select(spouse => (spouse.Person.Name!.FirstName, spouse.IsFormer)));
        Assert.IsType<MarriageFormerChanged>(_store.AuditEvents[^1].Event);
    }

    [Fact]
    [Trait("AC", "AC-05.4")]
    public async Task A_private_spouse_is_listed_as_a_placeholder()
    {
        var ram = AddPerson("Ram", Alice);
        var sita = AddPerson("Sita", Alice);
        _store.People[sita].SetVisibility(ProfileVisibility.Private, Alice);
        await Service().CreateAsync(ram, sita, Alice, Cancellation);

        var spouse = Assert.Single((await Service().ListSpousesAsync(ram, As(Bob), Cancellation)).Spouses);

        Assert.Null(spouse.Person.Name);
        Assert.Equal(PersonPlaceholder.Private, spouse.Person.Placeholder);
    }

    [Fact]
    [Trait("AC", "AC-02.16")]
    public async Task A_parent_and_child_cannot_marry_and_spouses_cannot_become_parent_and_child()
    {
        var hari = AddPerson("Hari", Alice);
        var maya = AddPerson("Maya", Alice);
        var ram = AddPerson("Ram", Alice);
        var relationships = new RelationshipService(
            _store, _store, _store, _store, _store, _store, _store, new FixedTimeProvider(Now), new RelationshipLimits());
        await relationships.CreateLinkAsync(hari, maya, ParentChildType.Biological, Alice, Cancellation);
        await Service().CreateAsync(maya, ram, Alice, Cancellation);

        await Assert.ThrowsAsync<ParentChildMarriageException>(() => Service().CreateAsync(maya, hari, Alice, Cancellation));
        await Assert.ThrowsAsync<LinkBetweenSpousesException>(
            () => relationships.CreateLinkAsync(ram, maya, ParentChildType.Adoptive, Alice, Cancellation));
    }

    [Fact]
    public async Task Spouses_of_an_unknown_person_are_not_found()
    {
        await Assert.ThrowsAsync<PersonNotFoundException>(() => Service().ListSpousesAsync(PersonId.New(), As(Alice), Cancellation));
    }

    [Fact]
    [Trait("AC", "AC-02.12")]
    public async Task A_sole_maintainer_deletes_the_marriage()
    {
        var ram = AddPerson("Ram", Alice);
        var sita = AddPerson("Sita", Alice);
        var created = await Service().CreateAsync(ram, sita, Alice, Cancellation);

        await Service().DeleteAsync(created.Id, Alice, Cancellation);

        Assert.True(_store.Marriages[created.Id].IsDeleted);
        Assert.Empty((await Service().ListSpousesAsync(ram, As(Alice), Cancellation)).Spouses);
        await Service().CreateAsync(ram, sita, Alice, Cancellation);
    }

    [Fact]
    public async Task Unknown_marriage_is_not_found()
    {
        await Assert.ThrowsAsync<MarriageNotFoundException>(() => Service().DeleteAsync(LinkId.New(), Alice, Cancellation));
    }

    private PersonId AddPerson(string firstName, AccountId maintainer)
    {
        var person = Person.Create(new PersonName(firstName), maintainer);
        person.TakeEvents();
        _store.People[person.Id] = person;
        return person.Id;
    }
}
