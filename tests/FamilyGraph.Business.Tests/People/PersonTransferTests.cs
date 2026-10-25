using FamilyGraph.Business.People;

namespace FamilyGraph.Business.Tests.People;

/// <summary>An administrator's transfer of a profile to another account (D-035).</summary>
public class PersonTransferTests
{
    private static readonly AccountId Alice = new(Guid.NewGuid());
    private static readonly AccountId Bob = new(Guid.NewGuid());
    private static readonly AccountId Admin = new(Guid.NewGuid());
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);

    private static Person NewPerson(AccountId maintainer)
    {
        var person = Person.Create(new PersonName("Maya"), maintainer);
        person.TakeEvents();
        return person;
    }

    [Fact]
    [Trait("AC", "AC-01.24")]
    [Trait("AC", "AC-01.26")]
    public void Administrator_makes_another_account_the_maintainer_and_the_event_names_both()
    {
        var person = NewPerson(Alice);

        person.Transfer(Bob, Admin, actorIsAdministrator: true);

        Assert.True(person.IsMaintainedBy(Bob));
        Assert.Equal([new PersonTransferred(person.Id, Admin, Alice, Bob)], person.PendingEvents);
    }

    [Fact]
    [Trait("AC", "AC-01.24")]
    public void Administrator_may_transfer_a_profile_to_themselves()
    {
        var person = NewPerson(Alice);

        person.Transfer(Admin, Admin, actorIsAdministrator: true);

        Assert.True(person.IsMaintainedBy(Admin));
    }

    [Fact]
    [Trait("AC", "AC-01.27")]
    public void Maintainer_who_is_not_an_administrator_cannot_transfer()
    {
        var person = NewPerson(Alice);

        Assert.Throws<TransferNotAdministratorException>(() => person.Transfer(Bob, Alice, actorIsAdministrator: false));

        Assert.True(person.IsMaintainedBy(Alice));
        Assert.Empty(person.PendingEvents);
    }

    [Fact]
    public void Transfer_to_the_current_maintainer_is_refused()
    {
        var person = NewPerson(Alice);

        var error = Assert.Throws<TransferToSameMaintainerException>(() => person.Transfer(Alice, Admin, actorIsAdministrator: true));

        Assert.Equal("transfer.same_maintainer", error.Code);
    }

    [Fact]
    public void A_deleted_profile_cannot_be_transferred()
    {
        var person = NewPerson(Alice);
        person.Delete(Alice, actorIsAdministrator: false, Now);

        Assert.Throws<ProfileDeletedException>(() => person.Transfer(Bob, Admin, actorIsAdministrator: true));
    }
}
