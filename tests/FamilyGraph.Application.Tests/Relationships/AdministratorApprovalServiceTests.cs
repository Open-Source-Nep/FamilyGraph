using FamilyGraph.Application.Privacy;
using FamilyGraph.Application.Relationships;
using FamilyGraph.Application.Tests.People;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Application.Tests.Relationships;

/// <summary>Administrators listing and approving pending sides on behalf of maintainers (D-040).</summary>
public class AdministratorApprovalServiceTests
{
    private static readonly AccountId Alice = new(Guid.NewGuid());
    private static readonly AccountId Bob = new(Guid.NewGuid());
    private static readonly AccountId Admin = new(Guid.NewGuid());
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);

    private static readonly Viewer Administrator = new(Admin, IsAdministrator: true);

    private readonly InMemoryPeople _store = new();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static Viewer As(AccountId account) => new(account, IsAdministrator: false);

    private RelationshipService Links(int limit = RelationshipLimits.DefaultInactiveLinksLimit) =>
        new(_store, _store, _store, _store, _store, _store, _store, new FixedTimeProvider(Now),
            new RelationshipLimits(RelationshipLimits.DefaultDirectRelativesLimit, limit));

    private MarriageService Marriages() =>
        new(_store, _store, _store, _store, _store, _store, new FixedTimeProvider(Now),
            new RelationshipLimits(RelationshipLimits.DefaultDirectRelativesLimit, RelationshipLimits.DefaultInactiveLinksLimit));

    [Fact]
    [Trait("AC", "AC-03.6")]
    [Trait("AC", "AC-03.8")]
    public async Task An_administrator_approves_a_pending_link_and_it_is_saved_with_its_event()
    {
        var parent = AddPerson("Hari", Alice);
        var child = AddPerson("Maya", Bob);
        var created = await Links().CreateLinkAsync(parent, child, ParentChildType.Biological, Alice, Cancellation);

        var approved = await Links().ApproveAsAdministratorAsync(created.Id, LinkEndpoint.Child, Administrator, Cancellation);

        Assert.True(approved.IsActive);
        Assert.True(_store.Links[created.Id].IsActive);
        var (auditEvent, occurredAt) = _store.AuditEvents[^1];
        Assert.Equal((new LinkEndpointApprovedByAdministrator(created.Id, child, Admin, Bob), Now), (auditEvent, occurredAt));
        Assert.Empty((await Links().ListPendingForAdministratorAsync(Administrator, Cancellation)).Links);
    }

    [Fact]
    [Trait("AC", "AC-03.6")]
    [Trait("AC", "AC-03.9")]
    public async Task Others_cannot_approve_on_behalf_or_list_every_pending_link()
    {
        var parent = AddPerson("Hari", Alice);
        var child = AddPerson("Maya", Bob);
        var created = await Links().CreateLinkAsync(parent, child, ParentChildType.Biological, Alice, Cancellation);
        var saves = _store.SaveCount;

        await Assert.ThrowsAsync<ApprovalNotAdministratorException>(
            () => Links().ApproveAsAdministratorAsync(created.Id, LinkEndpoint.Child, As(Alice), Cancellation));
        await Assert.ThrowsAsync<ApprovalNotAdministratorException>(
            () => Links().ListPendingForAdministratorAsync(As(Alice), Cancellation));
        await Assert.ThrowsAsync<ApprovalNotAdministratorException>(
            () => Marriages().ListPendingForAdministratorAsync(As(Bob), Cancellation));

        Assert.Equal(EndpointApproval.Pending, _store.Links[created.Id].ChildApproval);
        Assert.Equal(saves, _store.SaveCount);
    }

    [Fact]
    [Trait("AC", "AC-03.7")]
    public async Task An_administrator_cannot_restore_a_declined_side()
    {
        var parent = AddPerson("Hari", Alice);
        var child = AddPerson("Maya", Bob);
        var created = await Links().CreateLinkAsync(parent, child, ParentChildType.Biological, Alice, Cancellation);
        await Links().DisagreeAsync(created.Id, LinkEndpoint.Child, As(Bob), Cancellation);

        await Assert.ThrowsAsync<ApprovalNotPendingException>(
            () => Links().ApproveAsAdministratorAsync(created.Id, LinkEndpoint.Child, Administrator, Cancellation));

        Assert.False(_store.Links[created.Id].IsActive);
        Assert.Empty((await Links().ListPendingForAdministratorAsync(Administrator, Cancellation)).Links);
    }

    [Fact]
    [Trait("AC", "AC-03.9")]
    public async Task The_administrator_list_holds_links_waiting_for_other_maintainers_in_name_order()
    {
        var hari = AddPerson("Hari", Alice);
        var maya = AddPerson("Maya", Bob);
        var bina = AddPerson("Bina", Alice);
        var gita = AddPerson("Gita", Bob);
        var ownChild = AddPerson("Ram", Admin);
        var active = AddPerson("Sita", Alice);
        var service = Links();
        var waitingForBob = await service.CreateLinkAsync(hari, maya, ParentChildType.Biological, Alice, Cancellation);
        var waitingForAlice = await service.CreateLinkAsync(bina, gita, ParentChildType.Adoptive, Bob, Cancellation);
        await service.CreateLinkAsync(hari, ownChild, ParentChildType.Biological, Alice, Cancellation);
        await service.CreateLinkAsync(hari, active, ParentChildType.Biological, Alice, Cancellation);

        var list = await service.ListPendingForAdministratorAsync(Administrator, Cancellation);

        Assert.Equal([waitingForAlice.Id, waitingForBob.Id], list.Links.Select(link => link.Id));
        Assert.False(list.IsTruncated);
        var first = list.Links[0];
        Assert.Equal((EndpointApproval.Pending, false), (first.Parent.Approval, first.Parent.Person.MaintainedByViewer));
        Assert.Equal(EndpointApproval.Approved, first.Child.Approval);
    }

    [Fact]
    [Trait("AC", "AC-03.9")]
    public async Task The_administrator_list_is_cut_at_its_limit()
    {
        var hari = AddPerson("Hari", Alice);
        var service = Links(limit: 1);
        await service.CreateLinkAsync(hari, AddPerson("Maya", Bob), ParentChildType.Biological, Alice, Cancellation);
        await service.CreateLinkAsync(hari, AddPerson("Gita", Bob), ParentChildType.Biological, Alice, Cancellation);

        var list = await service.ListPendingForAdministratorAsync(Administrator, Cancellation);

        Assert.Single(list.Links);
        Assert.True(list.IsTruncated);
    }

    [Fact]
    [Trait("AC", "AC-03.6")]
    [Trait("AC", "AC-03.8")]
    [Trait("AC", "AC-03.9")]
    public async Task An_administrator_lists_and_approves_a_pending_marriage()
    {
        var ram = AddPerson("Ram", Alice);
        var sita = AddPerson("Sita", Bob);
        var created = await Marriages().CreateAsync(ram, sita, Alice, Cancellation);

        var waiting = Assert.Single((await Marriages().ListPendingForAdministratorAsync(Administrator, Cancellation)).Marriages);
        Assert.Equal(created.Id, waiting.Id);

        var approved = await Marriages().ApproveAsAdministratorAsync(created.Id, sita, Administrator, Cancellation);

        Assert.True(approved.IsActive);
        Assert.Equal(new MarriageSpouseApprovedByAdministrator(created.Id, sita, Admin, Bob), _store.AuditEvents[^1].Event);
        Assert.Empty((await Marriages().ListPendingForAdministratorAsync(Administrator, Cancellation)).Marriages);
        await Assert.ThrowsAsync<ApprovalNotAdministratorException>(
            () => Marriages().ApproveAsAdministratorAsync(created.Id, sita, As(Bob), Cancellation));
    }

    private PersonId AddPerson(string firstName, AccountId maintainer)
    {
        var person = Person.Create(new PersonName(firstName), maintainer);
        person.TakeEvents();
        _store.People[person.Id] = person;
        return person.Id;
    }
}
