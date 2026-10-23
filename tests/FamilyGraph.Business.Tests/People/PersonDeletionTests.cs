using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Business.Tests.People;

public class PersonDeletionTests
{
    private static readonly AccountId Alice = new(Guid.NewGuid());
    private static readonly AccountId Bob = new(Guid.NewGuid());
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlySet<PersonId> NoParents = new HashSet<PersonId>();

    private static AncestorSnapshot NoAncestors(Person person) => new(person.Id, new HashSet<PersonId>(), isComplete: true);

    private static Person NewPerson(string firstName, AccountId maintainer)
    {
        var person = Person.Create(new PersonName(firstName, "Devi", "Rai"), maintainer);
        person.TakeEvents();
        return person;
    }

    [Fact]
    [Trait("AC", "AC-05.10")]
    [Trait("AC", "AC-05.11")]
    public void Maintainer_deletes_the_profile_and_its_name_is_erased()
    {
        var person = NewPerson("Maya", Alice);

        person.Delete(Alice, actorIsAdministrator: false, Now);

        Assert.True(person.IsDeleted);
        Assert.Equal(Now, person.DeletedAt);
        Assert.Equal(PersonName.Erased, person.Name);
        Assert.Equal((null, null), (person.Name.MiddleName, person.Name.LastName));
        Assert.Equal([new PersonDeleted(person.Id, Alice, ProfileDeleter.Maintainer)], person.PendingEvents);
    }

    [Fact]
    [Trait("AC", "AC-05.11")]
    public void Administrator_deletes_someone_elses_profile_and_the_event_says_so()
    {
        var person = NewPerson("Maya", Alice);

        person.Delete(Bob, actorIsAdministrator: true, Now);

        Assert.Equal([new PersonDeleted(person.Id, Bob, ProfileDeleter.Administrator)], person.PendingEvents);
    }

    [Fact]
    [Trait("AC", "AC-05.11")]
    public void Administrator_deleting_their_own_profile_does_so_as_its_maintainer()
    {
        var person = NewPerson("Maya", Alice);

        person.Delete(Alice, actorIsAdministrator: true, Now);

        Assert.Equal(ProfileDeleter.Maintainer, Assert.IsType<PersonDeleted>(Assert.Single(person.PendingEvents)).DeletedBy);
    }

    [Fact]
    [Trait("AC", "AC-05.11")]
    public void Anyone_else_cannot_delete_the_profile()
    {
        var person = NewPerson("Maya", Alice);

        Assert.Throws<NotProfileMaintainerException>(() => person.Delete(Bob, actorIsAdministrator: false, Now));

        Assert.False(person.IsDeleted);
        Assert.Equal("Maya", person.Name.FirstName);
    }

    [Fact]
    [Trait("AC", "AC-05.10")]
    public void Deleted_profile_accepts_no_further_change()
    {
        var person = NewPerson("Maya", Alice);
        person.Delete(Alice, actorIsAdministrator: false, Now);
        person.TakeEvents();

        Assert.Equal("person.deleted", Assert.Throws<ProfileDeletedException>(
            () => person.Delete(Alice, actorIsAdministrator: false, Now)).Code);
        Assert.Throws<ProfileDeletedException>(() => person.Rename(new PersonName("Maya"), Alice));
        Assert.Throws<ProfileDeletedException>(() => person.SetVisibility(ProfileVisibility.Private, Alice));
        Assert.Equal(PersonName.Erased, person.Name);
        Assert.Empty(person.PendingEvents);
    }

    [Fact]
    [Trait("AC", "AC-05.12")]
    public void No_new_link_can_be_created_with_a_deleted_profile()
    {
        var parent = NewPerson("Hari", Alice);
        var child = NewPerson("Maya", Alice);
        child.Delete(Alice, actorIsAdministrator: false, Now);

        var ex = Assert.Throws<LinkToDeletedProfileException>(() =>
            ParentChildLink.Create(parent, child, ParentChildType.Biological, Alice, NoParents, NoAncestors(parent)));

        Assert.Equal("link.person_deleted", ex.Code);
    }

    [Fact]
    [Trait("AC", "AC-05.3")]
    [Trait("AC", "AC-05.4")]
    public void Deleting_a_profile_leaves_its_active_link_active()
    {
        var parent = NewPerson("Hari", Alice);
        var child = NewPerson("Maya", Bob);
        var link = ParentChildLink.Create(parent, child, ParentChildType.Adoptive, Alice, NoParents, NoAncestors(parent));
        link.Approve(child, Bob, NoAncestors(parent));

        parent.Delete(Alice, actorIsAdministrator: false, Now);

        Assert.True(link.IsActive);
    }

    [Fact]
    public void Consent_on_the_deleted_side_is_frozen_but_the_other_side_keeps_its_own()
    {
        var parent = NewPerson("Hari", Alice);
        var child = NewPerson("Maya", Bob);
        var link = ParentChildLink.Create(parent, child, ParentChildType.Biological, Bob, NoParents, NoAncestors(parent));
        child.Delete(Bob, actorIsAdministrator: false, Now);

        Assert.Throws<ProfileDeletedException>(() => link.Disagree(child, parent, Bob));
        Assert.Throws<ProfileDeletedException>(() => link.Approve(child, Bob, NoAncestors(parent)));
        link.Approve(parent, Alice, NoAncestors(parent));
        Assert.True(link.IsActive);

        link.Disagree(parent, child, Alice);
        Assert.False(link.IsActive);
        link.Approve(parent, Alice, NoAncestors(parent));
        Assert.True(link.IsActive);
    }

    [Fact]
    public void Sole_maintainer_can_still_delete_a_link_to_a_deleted_profile()
    {
        var parent = NewPerson("Hari", Alice);
        var child = NewPerson("Maya", Alice);
        var link = ParentChildLink.Create(parent, child, ParentChildType.Biological, Alice, NoParents, NoAncestors(parent));
        child.Delete(Alice, actorIsAdministrator: false, Now);

        link.Delete(parent, child, Alice, Now);

        Assert.True(link.IsDeleted);
    }
}
