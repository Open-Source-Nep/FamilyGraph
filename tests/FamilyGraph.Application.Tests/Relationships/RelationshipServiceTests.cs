using FamilyGraph.Application.People;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Application.Relationships;
using FamilyGraph.Application.Tests.People;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Application.Tests.Relationships;

public class RelationshipServiceTests
{
    private static readonly AccountId Alice = new(Guid.NewGuid());
    private static readonly AccountId Bob = new(Guid.NewGuid());
    private static readonly AccountId Admin = new(Guid.NewGuid());
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 30, 0, TimeSpan.Zero);

    private readonly InMemoryPeople _store = new();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static Viewer As(AccountId account) => new(account, IsAdministrator: false);

    private RelationshipService Service(
        int directRelativesLimit = RelationshipLimits.DefaultDirectRelativesLimit,
        int inactiveLinksLimit = RelationshipLimits.DefaultInactiveLinksLimit) =>
        new(
            _store,
            _store,
            _store,
            _store,
            _store,
            _store,
            new FixedTimeProvider(Now),
            new RelationshipLimits(directRelativesLimit, inactiveLinksLimit));

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

        var relatives = await service.ListDirectRelativesAsync(child, As(Bob), Cancellation);

        var parent = Assert.Single(relatives.Parents);
        Assert.Equal((hari, ParentChildType.Adoptive), (parent.Person.Id, parent.Type));
        Assert.True(relatives.ParentsTruncated);
        Assert.Empty(relatives.Children);
        Assert.False(relatives.ChildrenTruncated);
    }

    [Fact]
    [Trait("AC", "AC-05.1")]
    [Trait("AC", "AC-05.9")]
    public async Task Private_relatives_are_placeholders_with_their_link_type_listed_after_the_others()
    {
        var child = AddPerson("Maya", Alice);
        var hidden = AddPerson("Anita", Bob);
        var hari = AddPerson("Hari", Alice);
        var service = Service();
        var link = await service.CreateLinkAsync(hidden, child, ParentChildType.Adoptive, Alice, Cancellation);
        await service.ApproveAsync(link.Id, LinkEndpoint.Parent, As(Bob), Cancellation);
        await service.CreateLinkAsync(hari, child, ParentChildType.Biological, Alice, Cancellation);
        _store.People[hidden].SetVisibility(ProfileVisibility.Private, Bob);

        var relatives = await service.ListDirectRelativesAsync(child, As(Alice), Cancellation);

        // "Anita" would sort first by name; hidden people come last so the order hints at nothing.
        Assert.Equal(
            [(hari, "Hari", null, ParentChildType.Biological), (hidden, null, PersonPlaceholder.Private, ParentChildType.Adoptive)],
            relatives.Parents.Select(parent =>
                (parent.Person.Id, parent.Person.Name?.FirstName, parent.Person.Placeholder, parent.Type)));
        var asOwner = await service.ListDirectRelativesAsync(child, As(Bob), Cancellation);
        Assert.Equal(["Anita", "Hari"], asOwner.Parents.Select(parent => parent.Person.Name!.FirstName));
    }

    [Fact]
    [Trait("AC", "AC-05.3")]
    [Trait("AC", "AC-05.9")]
    [Trait("AC", "AC-05.12")]
    public async Task Deleted_relative_stays_as_a_placeholder_and_cannot_be_linked_again()
    {
        var child = AddPerson("Maya", Alice);
        var parent = AddPerson("Hari", Alice);
        var other = AddPerson("Gita", Alice);
        var service = Service();
        await service.CreateLinkAsync(parent, child, ParentChildType.Adoptive, Alice, Cancellation);
        _store.People[parent].Delete(Alice, actorIsAdministrator: false, Now);

        var relatives = await service.ListDirectRelativesAsync(child, As(Alice), Cancellation);

        var relative = Assert.Single(relatives.Parents);
        Assert.Equal((parent, null, PersonPlaceholder.Deleted, ParentChildType.Adoptive), (relative.Person.Id, relative.Person.Name, relative.Person.Placeholder, relative.Type));
        await Assert.ThrowsAsync<LinkToDeletedProfileException>(
            () => service.CreateLinkAsync(parent, other, ParentChildType.Biological, Alice, Cancellation));
    }

    [Fact]
    public async Task Pending_link_of_a_deleted_profile_leaves_its_former_maintainers_approvals()
    {
        var parent = AddPerson("Hari", Bob);
        var child = AddPerson("Maya", Alice);
        var service = Service();
        await service.CreateLinkAsync(parent, child, ParentChildType.Biological, Alice, Cancellation);
        _store.People[parent].Delete(Bob, actorIsAdministrator: false, Now);

        var asFormerMaintainer = await service.ListInactiveLinksAsync(As(Bob), Cancellation);
        var asOtherMaintainer = await service.ListInactiveLinksAsync(As(Alice), Cancellation);

        Assert.Empty(asFormerMaintainer.Links);
        Assert.Equal(PersonPlaceholder.Deleted, Assert.Single(asOtherMaintainer.Links).Parent.Person.Placeholder);
    }

    [Fact]
    [Trait("AC", "AC-05.5")]
    public async Task Making_an_endpoint_private_leaves_the_link_state_unchanged()
    {
        var parent = AddPerson("Hari", Bob);
        var child = AddPerson("Maya", Alice);
        var service = Service();
        var created = await service.CreateLinkAsync(parent, child, ParentChildType.Biological, Alice, Cancellation);

        _store.People[parent].SetVisibility(ProfileVisibility.Private, Bob);

        Assert.False(_store.Links[created.Id].IsActive);
        Assert.Empty((await service.ListDirectRelativesAsync(child, As(Alice), Cancellation)).Parents);
        var pending = Assert.Single((await service.ListInactiveLinksAsync(As(Alice), Cancellation)).Links);
        Assert.Equal((PersonPlaceholder.Private, EndpointApproval.Pending), (pending.Parent.Person.Placeholder, pending.Parent.Approval));
    }

    [Fact]
    public async Task Relatives_of_an_unknown_person_are_not_found()
    {
        await Assert.ThrowsAsync<PersonNotFoundException>(
            () => Service().ListDirectRelativesAsync(PersonId.New(), As(Alice), Cancellation));
    }

    [Fact]
    [Trait("AC", "AC-03.3")]
    public async Task Approving_the_pending_endpoint_activates_the_link_and_saves_its_event()
    {
        var parent = AddPerson("Hari", Bob);
        var child = AddPerson("Maya", Alice);
        var service = Service();
        var created = await service.CreateLinkAsync(parent, child, ParentChildType.Biological, Alice, Cancellation);

        var view = await service.ApproveAsync(created.Id, LinkEndpoint.Parent, As(Bob), Cancellation);

        Assert.True(view.IsActive);
        Assert.Equal(EndpointApproval.Approved, view.Parent.Approval);
        Assert.True(view.Parent.Person.MaintainedByViewer);
        Assert.False(view.Child.Person.MaintainedByViewer);
        Assert.Equal(new LinkEndpointApproved(created.Id, parent, Bob), _store.AuditEvents[^1].Event);
        Assert.Equal(Now, _store.AuditEvents[^1].OccurredAt);
        Assert.Equal(2, _store.SaveCount);
        var relatives = await service.ListDirectRelativesAsync(child, As(Alice), Cancellation);
        Assert.Equal(parent, Assert.Single(relatives.Parents).Person.Id);
    }

    [Fact]
    [Trait("AC", "AC-03.1")]
    [Trait("AC", "AC-03.3")]
    public async Task Withdrawing_deactivates_the_link_at_once_and_restoring_reactivates_it()
    {
        var parent = AddPerson("Hari", Bob);
        var child = AddPerson("Maya", Alice);
        var service = Service();
        var created = await service.CreateLinkAsync(parent, child, ParentChildType.Biological, Alice, Cancellation);
        await service.ApproveAsync(created.Id, LinkEndpoint.Parent, As(Bob), Cancellation);

        var withdrawn = await service.DisagreeAsync(created.Id, LinkEndpoint.Child, As(Alice), Cancellation);

        Assert.False(withdrawn.IsActive);
        Assert.Equal(EndpointApproval.Disagreed, withdrawn.Child.Approval);
        Assert.Equal(new LinkEndpointDisagreed(created.Id, child, Alice), _store.AuditEvents[^1].Event);
        Assert.Empty((await service.ListDirectRelativesAsync(child, As(Alice), Cancellation)).Parents);

        // Bob's approval is still valid, so restoring Alice's side is enough (PRD 03).
        var restored = await service.ApproveAsync(created.Id, LinkEndpoint.Child, As(Alice), Cancellation);

        Assert.True(restored.IsActive);
        Assert.Equal(new LinkApprovalRestored(created.Id, child, Alice), _store.AuditEvents[^1].Event);
        Assert.Single((await service.ListDirectRelativesAsync(child, As(Alice), Cancellation)).Parents);
    }

    [Fact]
    public async Task Declining_a_pending_endpoint_records_a_decline()
    {
        var parent = AddPerson("Hari", Bob);
        var child = AddPerson("Maya", Alice);
        var service = Service();
        var created = await service.CreateLinkAsync(parent, child, ParentChildType.Biological, Alice, Cancellation);

        var view = await service.DisagreeAsync(created.Id, LinkEndpoint.Parent, As(Bob), Cancellation);

        Assert.Equal(EndpointApproval.Disagreed, view.Parent.Approval);
        Assert.Equal(new LinkEndpointDeclined(created.Id, parent, Bob), _store.AuditEvents[^1].Event);
    }

    [Fact]
    [Trait("AC", "AC-03.5")]
    public async Task Administrator_or_other_maintainer_cannot_approve_an_endpoint_and_nothing_is_saved()
    {
        var parent = AddPerson("Hari", Bob);
        var child = AddPerson("Maya", Alice);
        var service = Service();
        var created = await service.CreateLinkAsync(parent, child, ParentChildType.Biological, Alice, Cancellation);

        await Assert.ThrowsAsync<NotConsentMaintainerException>(
            () => service.ApproveAsync(created.Id, LinkEndpoint.Parent, As(Admin), Cancellation));
        await Assert.ThrowsAsync<NotConsentMaintainerException>(
            () => service.ApproveAsync(created.Id, LinkEndpoint.Parent, As(Alice), Cancellation));

        Assert.False(_store.Links[created.Id].IsActive);
        Assert.Single(_store.AuditEvents);
        Assert.Equal(1, _store.SaveCount);
    }

    [Fact]
    [Trait("AC", "AC-02.6")]
    public async Task Approval_that_would_activate_a_link_into_a_cycle_is_rejected()
    {
        var ram = AddPerson("Ram", Bob);
        var hari = AddPerson("Hari", Alice);
        var maya = AddPerson("Maya", Alice);
        var service = Service();
        await service.CreateLinkAsync(ram, hari, ParentChildType.Biological, Alice, Cancellation);
        await service.CreateLinkAsync(hari, maya, ParentChildType.Biological, Alice, Cancellation);
        // A pending link Maya → Ram that slipped past the creation check, as two concurrent creations can (D-020).
        var looping = ParentChildLink.Create(
            _store.People[maya],
            _store.People[ram],
            ParentChildType.Biological,
            Alice,
            new HashSet<PersonId>(),
            new AncestorSnapshot(maya, new HashSet<PersonId>(), isComplete: true));
        looping.TakeEvents();
        _store.Links[looping.Id] = looping;

        await Assert.ThrowsAsync<LinkCycleDetectedException>(
            () => service.ApproveAsync(looping.Id, LinkEndpoint.Child, As(Bob), Cancellation));

        Assert.Equal(EndpointApproval.Pending, looping.ChildApproval);
    }

    [Fact]
    public async Task Sole_maintainer_cannot_disagree()
    {
        var parent = AddPerson("Hari", Alice);
        var child = AddPerson("Maya", Alice);
        var service = Service();
        var created = await service.CreateLinkAsync(parent, child, ParentChildType.Biological, Alice, Cancellation);

        await Assert.ThrowsAsync<SoleMaintainerMustDeleteException>(
            () => service.DisagreeAsync(created.Id, LinkEndpoint.Child, As(Alice), Cancellation));
    }

    [Fact]
    public async Task Unknown_link_is_not_found()
    {
        await Assert.ThrowsAsync<LinkNotFoundException>(
            () => Service().ApproveAsync(LinkId.New(), LinkEndpoint.Parent, As(Alice), Cancellation));
        await Assert.ThrowsAsync<LinkNotFoundException>(
            () => Service().DisagreeAsync(LinkId.New(), LinkEndpoint.Parent, As(Alice), Cancellation));
        await Assert.ThrowsAsync<LinkNotFoundException>(
            () => Service().DeleteAsync(LinkId.New(), Alice, Cancellation));
    }

    [Fact]
    [Trait("AC", "AC-03.4")]
    public async Task Sole_maintainer_deletes_the_link_and_it_leaves_every_list()
    {
        var parent = AddPerson("Hari", Alice);
        var child = AddPerson("Maya", Alice);
        var service = Service();
        var created = await service.CreateLinkAsync(parent, child, ParentChildType.Biological, Alice, Cancellation);

        await service.DeleteAsync(created.Id, Alice, Cancellation);

        Assert.Equal(Now, _store.Links[created.Id].DeletedAt);
        Assert.Equal(new LinkDeleted(created.Id, Now, Alice), _store.AuditEvents[^1].Event);
        Assert.Equal(2, _store.SaveCount);
        Assert.Empty((await service.ListDirectRelativesAsync(child, As(Alice), Cancellation)).Parents);
        Assert.Empty((await service.ListInactiveLinksAsync(As(Alice), Cancellation)).Links);
    }

    [Fact]
    [Trait("AC", "AC-03.4")]
    public async Task Different_maintainers_cannot_delete_and_withdrawal_records_a_distinct_event()
    {
        var parent = AddPerson("Hari", Bob);
        var child = AddPerson("Maya", Alice);
        var service = Service();
        var created = await service.CreateLinkAsync(parent, child, ParentChildType.Biological, Alice, Cancellation);
        await service.ApproveAsync(created.Id, LinkEndpoint.Parent, As(Bob), Cancellation);

        await Assert.ThrowsAsync<NotSoleMaintainerException>(
            () => service.DeleteAsync(created.Id, Alice, Cancellation));
        await Assert.ThrowsAsync<NotSoleMaintainerException>(
            () => service.DeleteAsync(created.Id, Admin, Cancellation));
        Assert.False(_store.Links[created.Id].IsDeleted);
        Assert.Equal(2, _store.SaveCount);

        await service.DisagreeAsync(created.Id, LinkEndpoint.Child, As(Alice), Cancellation);

        Assert.IsType<LinkEndpointDisagreed>(_store.AuditEvents[^1].Event);
        Assert.DoesNotContain(_store.AuditEvents, saved => saved.Event is LinkDeleted);
    }

    [Fact]
    public async Task Deleted_link_accepts_no_further_change()
    {
        var parent = AddPerson("Hari", Alice);
        var child = AddPerson("Maya", Alice);
        var service = Service();
        var created = await service.CreateLinkAsync(parent, child, ParentChildType.Biological, Alice, Cancellation);
        await service.DeleteAsync(created.Id, Alice, Cancellation);

        await Assert.ThrowsAsync<LinkDeletedException>(() => service.DeleteAsync(created.Id, Alice, Cancellation));
        await Assert.ThrowsAsync<LinkDeletedException>(
            () => service.ApproveAsync(created.Id, LinkEndpoint.Parent, As(Alice), Cancellation));
        Assert.Equal(2, _store.SaveCount);
    }

    [Fact]
    public async Task Deleted_link_can_be_created_again_as_a_new_link()
    {
        var parent = AddPerson("Hari", Alice);
        var child = AddPerson("Maya", Alice);
        var service = Service();
        var first = await service.CreateLinkAsync(parent, child, ParentChildType.Biological, Alice, Cancellation);
        await service.DeleteAsync(first.Id, Alice, Cancellation);

        var second = await service.CreateLinkAsync(parent, child, ParentChildType.Adoptive, Alice, Cancellation);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(parent, Assert.Single((await service.ListDirectRelativesAsync(child, As(Alice), Cancellation)).Parents).Person.Id);
    }

    [Fact]
    public async Task Inactive_links_of_your_profiles_list_those_waiting_for_you_first()
    {
        var maya = AddPerson("Maya", Alice);
        var hari = AddPerson("Hari", Alice);
        var bina = AddPerson("Bina", Bob);
        var gita = AddPerson("Gita", Bob);
        var ram = AddPerson("Ram", Bob);
        var service = Service();
        // Waiting for Bob: Alice links her Maya to Bob's Bina and Gita.
        await service.CreateLinkAsync(bina, maya, ParentChildType.Biological, Alice, Cancellation);
        await service.CreateLinkAsync(gita, maya, ParentChildType.Adoptive, Alice, Cancellation);
        // Waiting for Alice: Bob links his Ram to her Hari.
        var waitingForAlice = await service.CreateLinkAsync(ram, hari, ParentChildType.Biological, Bob, Cancellation);
        // Active and unrelated links are left out.
        await service.CreateLinkAsync(hari, maya, ParentChildType.Biological, Alice, Cancellation);
        await service.CreateLinkAsync(ram, gita, ParentChildType.Biological, Bob, Cancellation);

        var all = await service.ListInactiveLinksAsync(As(Alice), Cancellation);
        var cut = await Service(inactiveLinksLimit: 1).ListInactiveLinksAsync(As(Alice), Cancellation);

        Assert.Equal(
            ["Ram → Hari", "Bina → Maya", "Gita → Maya"],
            all.Links.Select(link => $"{link.Parent.Person.Name!.FirstName} → {link.Child.Person.Name!.FirstName}"));
        Assert.False(all.IsTruncated);
        Assert.Equal(waitingForAlice.Id, Assert.Single(cut.Links).Id);
        Assert.True(cut.IsTruncated);
        var first = all.Links[0];
        Assert.Equal((EndpointApproval.Approved, false), (first.Parent.Approval, first.Parent.Person.MaintainedByViewer));
        Assert.Equal((EndpointApproval.Pending, true), (first.Child.Approval, first.Child.Person.MaintainedByViewer));
    }

    private PersonId AddPerson(string firstName, AccountId maintainer)
    {
        var person = Person.Create(new PersonName(firstName), maintainer);
        person.TakeEvents();
        _store.People[person.Id] = person;
        return person.Id;
    }
}
