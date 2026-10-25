using FamilyGraph.Application.People;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Tests.People;

public class PersonDetailsServiceTests
{
    private static readonly AccountId Alice = new(Guid.NewGuid());
    private static readonly AccountId Bob = new(Guid.NewGuid());
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 9, 30, 0, TimeSpan.Zero);

    private readonly InMemoryPeople _people = new();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private PersonService Service() =>
        new(_people, _people, _people, _people, _people, new FixedTimeProvider(Now), new PeopleLimits());

    [Fact]
    [Trait("AC", "AC-01.30")]
    public async Task Create_saves_the_description_and_birth_date_hidden_from_others_by_default()
    {
        var service = Service();
        var id = await service.CreateAsync(
            new PersonName("Maya"), new PersonDetails(Description: "Teacher", BirthDate: BirthDate.Ad(1952, 3, 4)), Alice, Cancellation);

        var own = await service.GetAsync(id, new Viewer(Alice, false), Cancellation);
        var other = await service.GetAsync(id, new Viewer(Bob, false), Cancellation);

        Assert.Equal(("Teacher", BirthDate.Ad(1952, 3, 4), false), (own.Description, own.BirthDate, own.BirthDateShown));
        Assert.Equal(("Teacher", (BirthDate?)null), (other.Description, other.BirthDate));
    }

    [Fact]
    [Trait("AC", "AC-01.31")]
    [Trait("AC", "AC-01.32")]
    public async Task Maintainer_sets_the_description_and_birth_date_and_each_change_is_audited()
    {
        var service = Service();
        var id = await service.CreateAsync(new PersonName("Maya"), PersonDetails.None, Alice, Cancellation);

        await service.SetDescriptionAsync(id, "Engineer", Alice, Cancellation);
        await service.SetBirthDateAsync(id, BirthDate.Ad(1990), shown: true, Alice, Cancellation);

        var seen = await service.GetAsync(id, new Viewer(Bob, false), Cancellation);
        Assert.Equal(("Engineer", BirthDate.Ad(1990)), (seen.Description, seen.BirthDate));
        Assert.Equal(
            [(new PersonDescriptionChanged(id, Alice), Now), (new PersonBirthDateChanged(id, Alice, true), Now)],
            _people.AuditEvents[^2..]);
    }

    [Fact]
    [Trait("AC", "AC-01.31")]
    [Trait("AC", "AC-01.32")]
    public async Task Someone_other_than_the_maintainer_cannot_set_them()
    {
        var service = Service();
        var id = await service.CreateAsync(new PersonName("Maya"), PersonDetails.None, Alice, Cancellation);

        await Assert.ThrowsAsync<NotProfileMaintainerException>(() => service.SetDescriptionAsync(id, "Engineer", Bob, Cancellation));
        await Assert.ThrowsAsync<NotProfileMaintainerException>(
            () => service.SetBirthDateAsync(id, BirthDate.Ad(1990), true, Bob, Cancellation));
    }
}

public class BirthDatesTests
{
    [Fact]
    [Trait("AC", "AC-01.32")]
    public void A_full_bs_date_sorts_by_its_ad_day_and_a_partial_one_by_its_first_day()
    {
        var full = BirthDates.Create(BirthCalendar.Bs, 2082, 6, 16);
        var month = BirthDates.Create(BirthCalendar.Bs, 2082, 1, null);
        var year = BirthDates.Create(BirthCalendar.Bs, 2082, null, null);

        Assert.Equal((BirthCalendar.Bs, 2082, (int?)6, (int?)16), (full.Calendar, full.Year, full.Month, full.Day));
        Assert.Equal(new DateOnly(2025, 10, 2), full.SortDate);
        Assert.Equal(new DateOnly(2025, 4, 14), month.SortDate);
        Assert.Equal(month.SortDate, year.SortDate);
    }

    [Fact]
    [Trait("AC", "AC-01.32")]
    public void A_bs_day_that_does_not_exist_is_rejected()
    {
        Assert.Equal("person.birth_date_invalid", Assert.Throws<InvalidBirthDateException>(() => BirthDates.Create(BirthCalendar.Bs, 2081, 1, 32)).Code);
    }

    [Fact]
    [Trait("AC", "AC-01.32")]
    public void A_bs_year_before_the_conversion_table_sorts_by_an_estimate()
    {
        var old = BirthDates.Create(BirthCalendar.Bs, 1950, 2, null);

        Assert.Equal(new DateOnly(1893, 5, 13), old.SortDate);
        Assert.Equal(BirthDate.Ad(1890, 1, 1), BirthDates.Create(BirthCalendar.Ad, 1890, 1, 1));
    }
}
