using FamilyGraph.Business.People;

namespace FamilyGraph.Business.Tests.People;

public class PersonDetailsTests
{
    private static readonly AccountId Alice = new(Guid.NewGuid());
    private static readonly AccountId Bob = new(Guid.NewGuid());
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("AC", "AC-01.30")]
    public void A_person_is_created_with_or_without_a_description_and_birth_date_and_the_date_is_hidden_by_default()
    {
        var without = Person.Create(new PersonName("Maya"), Alice);
        var with = Person.Create(
            new PersonName("Ram"), Alice, description: "  Teacher, lives in Pokhara ", birthDate: BirthDate.Ad(1952, 3, 4));

        Assert.Equal((null, null, false), (without.Description, without.BirthDate, without.BirthDateShown));
        Assert.Equal(("Teacher, lives in Pokhara", BirthDate.Ad(1952, 3, 4), false), (with.Description, with.BirthDate, with.BirthDateShown));
        Assert.Equal([new PersonCreated(with.Id, Alice)], with.PendingEvents);
    }

    [Fact]
    [Trait("AC", "AC-01.31")]
    public void A_description_is_one_line_of_at_most_200_characters()
    {
        Assert.Null(PersonDescription.Normalize("   "));
        Assert.Equal(200, PersonDescription.Normalize(new string('a', 200))!.Length);
        Assert.Equal("person.description_too_long", Assert.Throws<DescriptionTooLongException>(() => PersonDescription.Normalize(new string('a', 201))).Code);
        Assert.Equal("person.description_not_one_line", Assert.Throws<DescriptionNotOneLineException>(() => PersonDescription.Normalize("one\ntwo")).Code);
    }

    [Fact]
    [Trait("AC", "AC-01.31")]
    public void Maintainer_sets_and_clears_the_description_and_each_change_is_audited_without_it()
    {
        var person = Person.Create(new PersonName("Maya"), Alice);
        person.TakeEvents();

        person.SetDescription("Engineer", Alice);
        var set = person.Description;
        person.SetDescription(" ", Alice);

        Assert.Equal(("Engineer", (string?)null), (set, person.Description));
        Assert.Equal([new PersonDescriptionChanged(person.Id, Alice), new PersonDescriptionChanged(person.Id, Alice)], person.PendingEvents);
    }

    [Fact]
    [Trait("AC", "AC-01.32")]
    public void A_birth_date_is_a_year_a_month_or_a_full_date_in_bs_or_ad()
    {
        var year = BirthDate.Ad(1890);
        var month = BirthDate.Ad(1952, 3);
        var bs = BirthDate.Bs(2009, 1, 32, new DateOnly(1952, 5, 14));

        Assert.Equal((BirthCalendar.Ad, 1890, (int?)null, (int?)null, new DateOnly(1890, 1, 1)), (year.Calendar, year.Year, year.Month, year.Day, year.SortDate));
        Assert.Equal(new DateOnly(1952, 3, 1), month.SortDate);
        Assert.Equal((BirthCalendar.Bs, true, new DateOnly(1952, 5, 14)), (bs.Calendar, bs.IsFull, bs.SortDate));
        Assert.Throws<InvalidBirthDateException>(() => BirthDate.Ad(1952, 2, 30));
        Assert.Throws<InvalidBirthDateException>(() => BirthDate.Ad(1952, 13));
        Assert.Throws<InvalidBirthDateException>(() => BirthDate.Ad(1952, null, 4));
        Assert.Throws<InvalidBirthDateException>(() => BirthDate.Ad(0));
        Assert.Equal("person.birth_date_invalid", Assert.Throws<InvalidBirthDateException>(() => BirthDate.Bs(2009, 1, 33, new DateOnly(1952, 5, 15))).Code);
    }

    [Fact]
    [Trait("AC", "AC-01.32")]
    public void Maintainer_sets_the_birth_date_and_its_switch_and_the_audit_records_only_the_switch()
    {
        var person = Person.Create(new PersonName("Maya"), Alice);
        person.TakeEvents();

        person.SetBirthDate(BirthDate.Ad(1990, 6), shown: true, Alice);
        var set = (person.BirthDate, person.BirthDateShown);
        person.SetBirthDate(null, shown: false, Alice);

        Assert.Equal((BirthDate.Ad(1990, 6), true), set);
        Assert.Equal(((BirthDate?)null, false), (person.BirthDate, person.BirthDateShown));
        Assert.Equal(
            [new PersonBirthDateChanged(person.Id, Alice, true), new PersonBirthDateChanged(person.Id, Alice, false)],
            person.PendingEvents);
    }

    [Fact]
    [Trait("AC", "AC-01.31")]
    [Trait("AC", "AC-01.32")]
    public void Someone_other_than_the_maintainer_cannot_change_the_description_or_birth_date()
    {
        var person = Person.Create(new PersonName("Maya"), Alice, description: "Engineer");
        person.TakeEvents();

        Assert.Throws<NotProfileMaintainerException>(() => person.SetDescription("Doctor", Bob));
        Assert.Throws<NotProfileMaintainerException>(() => person.SetBirthDate(BirthDate.Ad(1990), true, Bob));
        Assert.Equal(("Engineer", (BirthDate?)null), (person.Description, person.BirthDate));
        Assert.Empty(person.PendingEvents);
    }

    [Fact]
    [Trait("AC", "AC-05.15")]
    public void Deleting_a_profile_erases_its_description_and_birth_date()
    {
        var person = Person.Create(new PersonName("Maya"), Alice, description: "Engineer", birthDate: BirthDate.Ad(1990), birthDateShown: true);

        person.Delete(Alice, actorIsAdministrator: false, Now);

        Assert.Equal((null, null, false), (person.Description, person.BirthDate, person.BirthDateShown));
        Assert.Throws<ProfileDeletedException>(() => person.SetDescription("Engineer", Alice));
        Assert.Throws<ProfileDeletedException>(() => person.SetBirthDate(null, false, Alice));
    }
}
