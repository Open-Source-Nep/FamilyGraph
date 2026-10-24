using FamilyGraph.Business.Audit;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Business.Tests.Audit;

public class AuditEventTests
{
    private static readonly AccountId Alice = new(Guid.NewGuid());
    private static readonly AccountId Bob = new(Guid.NewGuid());

    private static readonly IReadOnlySet<PersonId> NoParents = new HashSet<PersonId>();

    private static readonly DateTimeOffset Now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private static AncestorSnapshot NoAncestors(Person person) => new(person.Id, new HashSet<PersonId>(), isComplete: true);

    private static Person NewPerson(string firstName, AccountId maintainer)
    {
        var person = Person.Create(new PersonName(firstName), maintainer);
        person.TakeEvents();
        return person;
    }

    [Fact]
    public void Creating_and_renaming_a_person_records_profile_events_without_names()
    {
        var person = Person.Create(new PersonName("Maya"), Alice);
        person.Rename(new PersonName("Gita"), Alice);

        Assert.Equal(
            [new PersonCreated(person.Id, Alice), new PersonRenamed(person.Id, Alice)],
            person.PendingEvents);
        Assert.All(person.PendingEvents, e => Assert.Equal(AuditFamily.Profile, e.Family));
    }

    [Fact]
    public void Rejected_operations_record_nothing()
    {
        var person = NewPerson("Maya", Alice);

        Assert.Throws<NotProfileMaintainerException>(() => person.Rename(new PersonName("Gita"), Bob));

        Assert.Empty(person.PendingEvents);
    }

    [Fact]
    [Trait("AC", "AC-07.1")]
    [Trait("AC", "AC-03.4")]
    public void A_links_lifecycle_can_be_reconstructed_from_its_events()
    {
        var parent = NewPerson("Hari", Alice);
        var child = NewPerson("Maya", Bob);

        var link = ParentChildLink.Create(parent, child, ParentChildType.Adoptive, Alice, NoParents, NoAncestors(parent), married: false);
        link.Approve(child, Bob, NoAncestors(parent));
        link.Disagree(child, parent, Bob);
        link.Approve(child, Bob, NoAncestors(parent));

        Assert.Equal(
            [
                new LinkCreated(link.Id, parent.Id, child.Id, ParentChildType.Adoptive, EndpointApproval.Approved, EndpointApproval.Pending, Alice),
                new LinkEndpointApproved(link.Id, child.Id, Bob),
                new LinkEndpointDisagreed(link.Id, child.Id, Bob),
                new LinkApprovalRestored(link.Id, child.Id, Bob),
            ],
            link.PendingEvents);
        Assert.All(link.PendingEvents, e => Assert.Equal(AuditFamily.RelationshipConsent, e.Family));
    }

    [Fact]
    public void Declining_a_pending_link_is_recorded_as_a_decline()
    {
        var parent = NewPerson("Hari", Alice);
        var child = NewPerson("Maya", Bob);
        var link = ParentChildLink.Create(parent, child, ParentChildType.Biological, Alice, NoParents, NoAncestors(parent), married: false);
        link.TakeEvents();

        link.Disagree(child, parent, Bob);

        Assert.Equal([new LinkEndpointDeclined(link.Id, child.Id, Bob)], link.PendingEvents);
    }

    [Fact]
    [Trait("AC", "AC-03.4")]
    public void Deletion_and_withdrawal_record_distinct_events()
    {
        var parent = NewPerson("Hari", Alice);
        var child = NewPerson("Maya", Alice);
        var deleted = ParentChildLink.Create(parent, child, ParentChildType.Biological, Alice, NoParents, NoAncestors(parent), married: false);
        deleted.TakeEvents();

        var otherChild = NewPerson("Sita", Bob);
        var withdrawn = ParentChildLink.Create(parent, otherChild, ParentChildType.Biological, Alice, NoParents, NoAncestors(parent), married: false);
        withdrawn.Approve(otherChild, Bob, NoAncestors(parent));
        withdrawn.TakeEvents();

        deleted.Delete(parent, child, Alice, Now);
        withdrawn.Disagree(parent, otherChild, Alice);

        Assert.Equal([new LinkDeleted(deleted.Id, Now, Alice)], deleted.PendingEvents);
        Assert.Equal([new LinkEndpointDisagreed(withdrawn.Id, parent.Id, Alice)], withdrawn.PendingEvents);
    }

    [Fact]
    [Trait("AC", "AC-07.2")]
    public void Recorded_events_do_not_make_an_inactive_link_active()
    {
        var parent = NewPerson("Hari", Alice);
        var child = NewPerson("Maya", Bob);
        var link = ParentChildLink.Create(parent, child, ParentChildType.Biological, Alice, NoParents, NoAncestors(parent), married: false);
        link.Approve(child, Bob, NoAncestors(parent));
        link.Disagree(child, parent, Bob);

        link.TakeEvents();

        Assert.False(link.IsActive);
    }

    [Fact]
    public void Taking_events_returns_them_once()
    {
        var person = Person.Create(new PersonName("Maya"), Alice);

        var taken = person.TakeEvents();

        Assert.Single(taken);
        Assert.Empty(person.PendingEvents);
        Assert.Empty(person.TakeEvents());
    }

    [Fact]
    [Trait("AC", "AC-07.3")]
    public void Claim_decisions_have_their_own_family_apart_from_consent()
    {
        Assert.NotEqual(AuditFamily.IdentityClaim, AuditFamily.RelationshipConsent);
        Assert.Equal([1, 2, 3, 4, 5], AuditFamily.All.Select(family => family.Value));
    }
}
