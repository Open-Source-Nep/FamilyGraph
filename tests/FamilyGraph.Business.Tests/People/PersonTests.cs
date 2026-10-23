using FamilyGraph.Business.People;

namespace FamilyGraph.Business.Tests.People;

public class PersonTests
{
    private static readonly AccountId Alice = new(Guid.NewGuid());
    private static readonly AccountId Bob = new(Guid.NewGuid());

    [Fact]
    [Trait("AC", "AC-01.1")]
    [Trait("AC", "AC-01.7")]
    public void Person_is_created_with_only_a_first_name_and_no_account_of_their_own()
    {
        var person = Person.Create(new PersonName("Maya"), Alice);

        Assert.Equal("Maya", person.Name.FirstName);
        Assert.NotEqual(Guid.Empty, person.Id.Value);
    }

    [Fact]
    [Trait("AC", "AC-01.3")]
    public void Creator_becomes_the_single_maintainer()
    {
        var person = Person.Create(new PersonName("Maya"), Alice);

        Assert.Equal(Alice, person.MaintainerId);
        Assert.True(person.IsMaintainedBy(Alice));
        Assert.False(person.IsMaintainedBy(Bob));
    }

    [Fact]
    [Trait("AC", "AC-01.2")]
    public void One_account_can_maintain_many_profiles()
    {
        var first = Person.Create(new PersonName("Maya"), Alice);
        var second = Person.Create(new PersonName("Gita"), Alice);

        Assert.NotEqual(first.Id, second.Id);
        Assert.True(first.IsMaintainedBy(Alice));
        Assert.True(second.IsMaintainedBy(Alice));
    }

    [Fact]
    [Trait("AC", "AC-01.6")]
    public void People_with_the_same_name_are_distinct_profiles()
    {
        var first = Person.Create(new PersonName("Ram", lastName: "Thapa"), Alice);
        var second = Person.Create(new PersonName("Ram", lastName: "Thapa"), Alice);

        Assert.Equal(first.Name, second.Name);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    [Trait("AC", "AC-01.8")]
    public void Maintainer_can_rename_the_person()
    {
        var person = Person.Create(new PersonName("Maya"), Alice);

        person.Rename(new PersonName("Maya", lastName: "Gurung"), Alice);

        Assert.Equal("Gurung", person.Name.LastName);
    }

    [Fact]
    [Trait("AC", "AC-01.8")]
    public void Someone_other_than_the_maintainer_cannot_rename_the_person()
    {
        var person = Person.Create(new PersonName("Maya"), Alice);

        var ex = Assert.Throws<NotProfileMaintainerException>(
            () => person.Rename(new PersonName("Impostor"), Bob));

        Assert.Equal("person.not_maintainer", ex.Code);
        Assert.Equal("Maya", person.Name.FirstName);
    }

    [Fact]
    [Trait("AC", "AC-05.7")]
    public void New_profile_is_public_and_its_maintainer_can_make_it_private()
    {
        var person = Person.Create(new PersonName("Maya"), Alice);
        Assert.Equal(ProfileVisibility.Public, person.Visibility);

        person.SetVisibility(ProfileVisibility.Private, Alice);

        Assert.Equal(ProfileVisibility.Private, person.Visibility);
        Assert.Contains(new PersonVisibilityChanged(person.Id, Alice, ProfileVisibility.Private), person.PendingEvents);
    }

    [Fact]
    [Trait("AC", "AC-05.7")]
    public void Someone_other_than_the_maintainer_cannot_change_the_visibility()
    {
        var person = Person.Create(new PersonName("Maya"), Alice);

        var ex = Assert.Throws<NotProfileMaintainerException>(
            () => person.SetVisibility(ProfileVisibility.Private, Bob));

        Assert.Equal("person.not_maintainer", ex.Code);
        Assert.Equal(ProfileVisibility.Public, person.Visibility);
    }

    [Fact]
    public void Setting_the_current_visibility_again_is_a_conflict()
    {
        var person = Person.Create(new PersonName("Maya"), Alice);
        person.TakeEvents();

        var ex = Assert.Throws<ProfileVisibilityUnchangedException>(
            () => person.SetVisibility(ProfileVisibility.Public, Alice));

        Assert.Equal("person.visibility_unchanged", ex.Code);
        Assert.Empty(person.PendingEvents);
    }

    [Fact]
    public void Empty_account_id_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new AccountId(Guid.Empty));
    }
}
