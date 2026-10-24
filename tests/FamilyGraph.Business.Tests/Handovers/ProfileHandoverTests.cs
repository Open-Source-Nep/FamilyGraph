using FamilyGraph.Business.Handovers;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Business.Tests.Handovers;

public class ProfileHandoverTests
{
    private static readonly AccountId Alice = new(Guid.NewGuid());
    private static readonly AccountId Bob = new(Guid.NewGuid());
    private static readonly AccountId Carol = new(Guid.NewGuid());
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Expiry = Now.AddDays(14);

    private static Person NewPerson(string firstName, AccountId maintainer)
    {
        var person = Person.Create(new PersonName(firstName, null, "Rai"), maintainer);
        person.TakeEvents();
        return person;
    }

    private static ProfileHandover Offer(Person person, AccountId actor)
    {
        var handover = ProfileHandover.Offer(person, actor, "hash", Now, Expiry);
        handover.TakeEvents();
        return handover;
    }

    [Fact]
    [Trait("AC", "AC-01.13")]
    public void Maintainer_creates_an_open_handover_link()
    {
        var person = NewPerson("Maya", Alice);

        var handover = ProfileHandover.Offer(person, Alice, "hash", Now, Expiry);

        Assert.Equal(HandoverState.Open, handover.State);
        Assert.Equal((person.Id, Alice, "hash", Now, Expiry), (handover.PersonId, handover.OfferedById, handover.TokenHash, handover.CreatedAt, handover.ExpiresAt));
        Assert.Equal([new HandoverOffered(handover.Id, person.Id, Alice, Expiry)], handover.PendingEvents);
        Assert.True(handover.CanBeAccepted(person, Now));
    }

    [Fact]
    [Trait("AC", "AC-01.13")]
    public void Only_the_maintainer_creates_a_handover_link()
    {
        var person = NewPerson("Maya", Alice);

        var error = Assert.Throws<NotProfileMaintainerException>(() => ProfileHandover.Offer(person, Bob, "hash", Now, Expiry));

        Assert.Equal("person.not_maintainer", error.Code);
    }

    [Fact]
    [Trait("AC", "AC-01.13")]
    public void A_deleted_profile_cannot_be_handed_over()
    {
        var person = NewPerson("Maya", Alice);
        person.Delete(Alice, actorIsAdministrator: false, Now);

        Assert.Throws<ProfileDeletedException>(() => ProfileHandover.Offer(person, Alice, "hash", Now, Expiry));
    }

    [Fact]
    public void A_handover_link_must_expire_after_it_is_created()
    {
        var person = NewPerson("Maya", Alice);

        Assert.Throws<ArgumentOutOfRangeException>(() => ProfileHandover.Offer(person, Alice, "hash", Now, Now));
    }

    [Fact]
    [Trait("AC", "AC-01.14")]
    [Trait("AC", "AC-01.3")]
    public void Accepting_makes_the_receiver_the_only_maintainer()
    {
        var person = NewPerson("Maya", Alice);
        var handover = Offer(person, Alice);

        person.AcceptHandover(handover, Bob, Now.AddDays(1));

        Assert.Equal(Bob, person.MaintainerId);
        Assert.False(person.IsMaintainedBy(Alice));
        Assert.Equal((HandoverState.Accepted, (AccountId?)Bob, (DateTimeOffset?)Now.AddDays(1)), (handover.State, handover.AcceptedById, handover.ClosedAt));
        Assert.Equal([new PersonHandedOver(person.Id, Bob, Alice, handover.Id)], person.PendingEvents);
        Assert.Throws<NotProfileMaintainerException>(() => person.Rename(new PersonName("Mira", null, null), Alice));
    }

    [Fact]
    [Trait("AC", "AC-01.15")]
    public void Accepting_leaves_links_and_their_consent_unchanged()
    {
        var parent = NewPerson("Maya", Alice);
        var child = NewPerson("Asha", Carol);
        var link = ParentChildLink.Create(
            parent, child, ParentChildType.Biological, Alice, new HashSet<PersonId>(), new AncestorSnapshot(parent.Id, new HashSet<PersonId>(), isComplete: true), married: false);
        link.TakeEvents();
        var handover = Offer(parent, Alice);

        parent.AcceptHandover(handover, Bob, Now);

        Assert.Equal((EndpointApproval.Approved, EndpointApproval.Pending), (link.ParentApproval, link.ChildApproval));
        Assert.Empty(link.PendingEvents);
    }

    [Fact]
    [Trait("AC", "AC-01.16")]
    public void A_used_link_cannot_be_accepted_again()
    {
        var person = NewPerson("Maya", Alice);
        var handover = Offer(person, Alice);
        person.AcceptHandover(handover, Bob, Now);

        var error = Assert.Throws<HandoverClosedException>(() => person.AcceptHandover(handover, Carol, Now));

        Assert.Equal("handover.closed", error.Code);
        Assert.Equal(Bob, person.MaintainerId);
    }

    [Fact]
    [Trait("AC", "AC-01.16")]
    public void An_expired_link_cannot_be_accepted()
    {
        var person = NewPerson("Maya", Alice);
        var handover = Offer(person, Alice);

        var error = Assert.Throws<HandoverExpiredException>(() => person.AcceptHandover(handover, Bob, Expiry));

        Assert.Equal("handover.expired", error.Code);
        Assert.Equal(Alice, person.MaintainerId);
        Assert.False(handover.CanBeAccepted(person, Expiry));
    }

    [Fact]
    [Trait("AC", "AC-01.16")]
    public void A_cancelled_link_cannot_be_accepted()
    {
        var person = NewPerson("Maya", Alice);
        var handover = Offer(person, Alice);

        handover.Cancel(person, Alice, Now);

        Assert.Equal((HandoverState.Cancelled, (DateTimeOffset?)Now), (handover.State, handover.ClosedAt));
        Assert.Equal([new HandoverCancelled(handover.Id, person.Id, Alice)], handover.PendingEvents);
        Assert.Throws<HandoverClosedException>(() => person.AcceptHandover(handover, Bob, Now));
    }

    [Fact]
    [Trait("AC", "AC-01.16")]
    public void A_link_is_void_once_its_creator_no_longer_maintains_the_profile()
    {
        var person = NewPerson("Maya", Alice);
        var stale = Offer(person, Alice);
        var other = Offer(person, Alice);
        person.AcceptHandover(other, Bob, Now);

        Assert.False(stale.CanBeAccepted(person, Now));
        Assert.Throws<HandoverClosedException>(() => person.AcceptHandover(stale, Carol, Now));
    }

    [Fact]
    [Trait("AC", "AC-01.16")]
    public void Only_the_maintainer_cancels_a_link_and_only_once()
    {
        var person = NewPerson("Maya", Alice);
        var handover = Offer(person, Alice);

        Assert.Throws<NotProfileMaintainerException>(() => handover.Cancel(person, Bob, Now));
        handover.Cancel(person, Alice, Now);
        Assert.Throws<HandoverClosedException>(() => handover.Cancel(person, Alice, Now));
    }

    [Fact]
    [Trait("AC", "AC-01.14")]
    public void The_maintainer_cannot_accept_their_own_link()
    {
        var person = NewPerson("Maya", Alice);
        var handover = Offer(person, Alice);

        var error = Assert.Throws<HandoverOwnProfileException>(() => person.AcceptHandover(handover, Alice, Now));

        Assert.Equal("handover.own_profile", error.Code);
        Assert.Equal(HandoverState.Open, handover.State);
        Assert.Empty(person.PendingEvents);
    }

    [Fact]
    [Trait("AC", "AC-01.16")]
    public void A_deleted_profile_cannot_be_accepted()
    {
        var person = NewPerson("Maya", Alice);
        var handover = Offer(person, Alice);
        person.Delete(Alice, actorIsAdministrator: false, Now);

        Assert.Throws<ProfileDeletedException>(() => person.AcceptHandover(handover, Bob, Now));
    }

    [Fact]
    public void A_link_for_another_profile_is_refused()
    {
        var person = NewPerson("Maya", Alice);
        var other = NewPerson("Asha", Alice);
        var handover = Offer(other, Alice);

        Assert.Throws<ArgumentException>(() => person.AcceptHandover(handover, Bob, Now));
    }
}
