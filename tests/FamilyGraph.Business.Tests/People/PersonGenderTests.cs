using FamilyGraph.Business.People;

namespace FamilyGraph.Business.Tests.People;

public class PersonGenderTests
{
    private static readonly AccountId Alice = new(Guid.NewGuid());
    private static readonly AccountId Bob = new(Guid.NewGuid());
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("AC", "AC-01.21")]
    public void A_person_is_created_without_a_gender_unless_one_is_given()
    {
        var without = Person.Create(new PersonName("Maya"), Alice);
        var with = Person.Create(new PersonName("Ram"), Alice, Gender.Male);

        Assert.Null(without.Gender);
        Assert.Equal(Gender.Male, with.Gender);
        Assert.Equal([new PersonCreated(with.Id, Alice)], with.PendingEvents);
    }

    [Fact]
    [Trait("AC", "AC-01.21")]
    public void The_genders_are_male_female_and_other()
    {
        Assert.Equal(["Male", "Female", "Other"], Gender.All.Select(gender => gender.Name));
    }

    [Fact]
    [Trait("AC", "AC-01.22")]
    public void Maintainer_sets_changes_and_clears_the_gender_and_each_change_is_audited_without_it()
    {
        var person = Person.Create(new PersonName("Maya"), Alice);
        person.TakeEvents();

        person.SetGender(Gender.Female, Alice);
        var set = person.Gender;
        person.SetGender(Gender.Other, Alice);
        var changed = person.Gender;
        person.SetGender(null, Alice);

        Assert.Equal((Gender.Female, Gender.Other, (Gender?)null), (set, changed, person.Gender));
        Assert.Equal(
            [new PersonGenderChanged(person.Id, Alice), new PersonGenderChanged(person.Id, Alice), new PersonGenderChanged(person.Id, Alice)],
            person.PendingEvents);
    }

    [Fact]
    [Trait("AC", "AC-01.22")]
    public void Someone_other_than_the_maintainer_cannot_change_the_gender()
    {
        var person = Person.Create(new PersonName("Maya"), Alice, Gender.Female);
        person.TakeEvents();

        Assert.Equal("person.not_maintainer", Assert.Throws<NotProfileMaintainerException>(() => person.SetGender(Gender.Male, Bob)).Code);
        Assert.Equal(Gender.Female, person.Gender);
        Assert.Empty(person.PendingEvents);
    }

    [Fact]
    [Trait("AC", "AC-05.14")]
    public void Deleting_a_profile_erases_its_gender_and_it_cannot_be_set_again()
    {
        var person = Person.Create(new PersonName("Maya"), Alice, Gender.Female);

        person.Delete(Alice, actorIsAdministrator: false, Now);

        Assert.Null(person.Gender);
        Assert.Throws<ProfileDeletedException>(() => person.SetGender(Gender.Female, Alice));
    }
}
