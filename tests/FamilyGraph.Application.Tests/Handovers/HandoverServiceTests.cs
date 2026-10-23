using FamilyGraph.Application.Handovers;
using FamilyGraph.Application.People;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Application.Tests.People;
using FamilyGraph.Business.Handovers;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Application.Tests.Handovers;

public class HandoverServiceTests
{
    private static readonly AccountId Alice = new(Guid.NewGuid());
    private static readonly AccountId Bob = new(Guid.NewGuid());
    private static readonly AccountId Carol = new(Guid.NewGuid());
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    private readonly InMemoryPeople _people = new();
    private readonly InMemoryHandovers _handovers = new();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static Viewer As(AccountId account) => new(account, IsAdministrator: false);

    private HandoverService Service(DateTimeOffset? now = null) =>
        new(_people, _people, _handovers, _people, _people, new FixedTimeProvider(now ?? Now), new HandoverSettings());

    private Person AddPerson(string firstName, AccountId maintainer)
    {
        var person = Person.Create(new PersonName(firstName), maintainer);
        person.TakeEvents();
        _people.People[person.Id] = person;
        return person;
    }

    [Fact]
    [Trait("AC", "AC-01.13")]
    public async Task Offer_creates_a_link_that_expires_after_the_lifetime_and_stores_only_its_hash()
    {
        var person = AddPerson("Maya", Alice);

        var created = await Service().OfferAsync(person.Id, Alice, Cancellation);

        var handover = Assert.Single(_handovers.Handovers);
        Assert.Equal(Now.AddDays(HandoverSettings.DefaultLinkLifetimeDays), created.ExpiresAt);
        Assert.NotEqual(created.Token, handover.TokenHash);
        Assert.Equal(HandoverToken.HashOf(created.Token), handover.TokenHash);
        Assert.Equal(new HandoverOffered(handover.Id, person.Id, Alice, created.ExpiresAt), Assert.Single(_people.AuditEvents).Event);
        Assert.Equal(new OpenHandover(Now, created.ExpiresAt), await Service().GetOpenAsync(person.Id, Alice, Cancellation));
    }

    [Fact]
    [Trait("AC", "AC-01.16")]
    public async Task A_new_link_replaces_the_open_one()
    {
        var person = AddPerson("Maya", Alice);
        var first = await Service().OfferAsync(person.Id, Alice, Cancellation);

        var second = await Service().OfferAsync(person.Id, Alice, Cancellation);

        Assert.Equal(HandoverState.Cancelled, _handovers.Handovers[0].State);
        Assert.IsType<HandoverCancelled>(_people.AuditEvents[^2].Event);
        await Assert.ThrowsAsync<HandoverClosedException>(() => Service().AcceptAsync(first.Token, Bob, Cancellation));
        await Service().AcceptAsync(second.Token, Bob, Cancellation);
        Assert.True(person.IsMaintainedBy(Bob));
    }

    [Fact]
    [Trait("AC", "AC-01.13")]
    public async Task Only_the_maintainer_offers_sees_or_cancels_the_link()
    {
        var person = AddPerson("Maya", Alice);
        await Service().OfferAsync(person.Id, Alice, Cancellation);

        await Assert.ThrowsAsync<NotProfileMaintainerException>(() => Service().OfferAsync(person.Id, Bob, Cancellation));
        await Assert.ThrowsAsync<NotProfileMaintainerException>(() => Service().GetOpenAsync(person.Id, Bob, Cancellation));
        await Assert.ThrowsAsync<NotProfileMaintainerException>(() => Service().CancelAsync(person.Id, Bob, Cancellation));
    }

    [Fact]
    public async Task Without_an_open_link_get_is_empty_and_cancel_is_not_found()
    {
        var person = AddPerson("Maya", Alice);

        Assert.Null(await Service().GetOpenAsync(person.Id, Alice, Cancellation));
        await Assert.ThrowsAsync<HandoverNotFoundException>(() => Service().CancelAsync(person.Id, Alice, Cancellation));
        await Assert.ThrowsAsync<PersonNotFoundException>(() => Service().OfferAsync(PersonId.New(), Alice, Cancellation));
    }

    [Fact]
    [Trait("AC", "AC-01.16")]
    public async Task Cancel_closes_the_link()
    {
        var person = AddPerson("Maya", Alice);
        var created = await Service().OfferAsync(person.Id, Alice, Cancellation);

        await Service().CancelAsync(person.Id, Alice, Cancellation);

        Assert.Null(await Service().GetOpenAsync(person.Id, Alice, Cancellation));
        Assert.IsType<HandoverCancelled>(_people.AuditEvents[^1].Event);
        await Assert.ThrowsAsync<HandoverClosedException>(() => Service().PreviewAsync(created.Token, As(Bob), Cancellation));
    }

    [Fact]
    [Trait("AC", "AC-01.14")]
    [Trait("AC", "AC-05.1")]
    public async Task Preview_shows_the_profile_as_the_receiver_may_see_it()
    {
        var person = AddPerson("Maya", Alice);
        var created = await Service().OfferAsync(person.Id, Alice, Cancellation);

        var preview = await Service().PreviewAsync(created.Token, As(Bob), Cancellation);
        Assert.Equal((person.Id, new PersonName("Maya"), created.ExpiresAt), (preview.Person.Id, preview.Person.Name, preview.ExpiresAt));

        person.SetVisibility(ProfileVisibility.Private, Alice);
        var hidden = await Service().PreviewAsync(created.Token, As(Bob), Cancellation);
        Assert.Equal((null, PersonPlaceholder.Private), (hidden.Person.Name, hidden.Person.Placeholder));
    }

    [Fact]
    [Trait("AC", "AC-01.14")]
    public async Task Preview_refuses_the_maintainers_own_link_and_unknown_tokens()
    {
        var person = AddPerson("Maya", Alice);
        var created = await Service().OfferAsync(person.Id, Alice, Cancellation);

        await Assert.ThrowsAsync<HandoverOwnProfileException>(() => Service().PreviewAsync(created.Token, As(Alice), Cancellation));
        await Assert.ThrowsAsync<HandoverNotFoundException>(() => Service().PreviewAsync("not-a-token", As(Bob), Cancellation));
        await Assert.ThrowsAsync<HandoverNotFoundException>(() => Service().AcceptAsync("", Bob, Cancellation));
    }

    [Fact]
    [Trait("AC", "AC-01.14")]
    [Trait("AC", "AC-01.15")]
    public async Task Accept_makes_the_receiver_the_maintainer_and_leaves_consent_unchanged()
    {
        var parent = AddPerson("Maya", Alice);
        var child = AddPerson("Asha", Carol);
        var link = ParentChildLink.Create(
            parent, child, ParentChildType.Adoptive, Alice, new HashSet<PersonId>(), new AncestorSnapshot(parent.Id, new HashSet<PersonId>(), isComplete: true));
        _people.Links[link.Id] = link;
        var created = await Service().OfferAsync(parent.Id, Alice, Cancellation);

        var accepted = await Service(Now.AddDays(2)).AcceptAsync(created.Token, Bob, Cancellation);

        Assert.Equal(parent.Id, accepted.PersonId);
        Assert.True(parent.IsMaintainedBy(Bob));
        Assert.Equal((EndpointApproval.Approved, EndpointApproval.Pending), (link.ParentApproval, link.ChildApproval));
        var (auditEvent, occurredAt) = _people.AuditEvents[^1];
        Assert.Equal(new PersonHandedOver(parent.Id, Bob, Alice, _handovers.Handovers[0].Id), auditEvent);
        Assert.Equal(Now.AddDays(2), occurredAt);
    }

    [Fact]
    [Trait("AC", "AC-01.16")]
    public async Task An_expired_link_cannot_be_previewed_or_accepted()
    {
        var person = AddPerson("Maya", Alice);
        var created = await Service().OfferAsync(person.Id, Alice, Cancellation);
        var later = Now.AddDays(HandoverSettings.DefaultLinkLifetimeDays);

        await Assert.ThrowsAsync<HandoverExpiredException>(() => Service(later).PreviewAsync(created.Token, As(Bob), Cancellation));
        await Assert.ThrowsAsync<HandoverExpiredException>(() => Service(later).AcceptAsync(created.Token, Bob, Cancellation));
        Assert.True(person.IsMaintainedBy(Alice));
    }
}
