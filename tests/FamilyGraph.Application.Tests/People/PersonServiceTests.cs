using FamilyGraph.Application.People;
using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Tests.People;

public class PersonServiceTests
{
    private static readonly AccountId Alice = new(Guid.NewGuid());
    private static readonly AccountId Bob = new(Guid.NewGuid());
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 30, 0, TimeSpan.Zero);

    private readonly InMemoryPeople _people = new();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private PersonService Service(int maintainedListLimit = PeopleLimits.DefaultMaintainedListLimit) =>
        new(_people, _people, _people, _people, new FixedTimeProvider(Now), new PeopleLimits(maintainedListLimit));

    [Fact]
    [Trait("AC", "AC-01.1")]
    [Trait("AC", "AC-01.7")]
    public async Task Create_saves_a_profile_with_only_a_first_name_maintained_by_the_creator()
    {
        var id = await Service().CreateAsync(new PersonName("Maya"), Alice, Cancellation);

        var person = _people.People[id];
        Assert.Equal(new PersonName("Maya"), person.Name);
        Assert.True(person.IsMaintainedBy(Alice));
        Assert.Equal(1, _people.SaveCount);
    }

    [Fact]
    public async Task Create_saves_the_creation_audit_event_with_the_time()
    {
        var id = await Service().CreateAsync(new PersonName("Maya"), Alice, Cancellation);

        var (auditEvent, occurredAt) = Assert.Single(_people.AuditEvents);
        Assert.Equal(new PersonCreated(id, Alice), auditEvent);
        Assert.Equal(Now, occurredAt);
    }

    [Fact]
    [Trait("AC", "AC-01.8")]
    public async Task Maintainer_renames_the_profile_and_the_rename_is_audited()
    {
        var service = Service();
        var id = await service.CreateAsync(new PersonName("Maya"), Alice, Cancellation);

        await service.RenameAsync(id, new PersonName("Maya", "Devi", "Rai"), Alice, Cancellation);

        Assert.Equal(new PersonName("Maya", "Devi", "Rai"), _people.People[id].Name);
        Assert.Equal(new PersonRenamed(id, Alice), _people.AuditEvents[^1].Event);
    }

    [Fact]
    [Trait("AC", "AC-01.8")]
    public async Task Rename_by_someone_else_is_refused_and_nothing_is_saved()
    {
        var service = Service();
        var id = await service.CreateAsync(new PersonName("Maya"), Alice, Cancellation);

        await Assert.ThrowsAsync<NotProfileMaintainerException>(
            () => service.RenameAsync(id, new PersonName("Gita"), Bob, Cancellation));

        Assert.Equal(new PersonName("Maya"), _people.People[id].Name);
        Assert.Single(_people.AuditEvents);
        Assert.Equal(1, _people.SaveCount);
    }

    [Fact]
    public async Task Rename_of_an_unknown_profile_is_not_found()
    {
        await Assert.ThrowsAsync<PersonNotFoundException>(
            () => Service().RenameAsync(PersonId.New(), new PersonName("Gita"), Alice, Cancellation));
    }

    [Fact]
    public async Task Any_account_can_view_a_profile_and_learns_only_whether_it_maintains_it()
    {
        var service = Service();
        var id = await service.CreateAsync(new PersonName("Maya"), Alice, Cancellation);

        var asMaintainer = await service.GetAsync(id, Alice, Cancellation);
        var asOther = await service.GetAsync(id, Bob, Cancellation);

        Assert.True(asMaintainer.MaintainedByViewer);
        Assert.False(asOther.MaintainedByViewer);
        Assert.Equal(new PersonName("Maya"), asOther.Name);
    }

    [Fact]
    public async Task Viewing_an_unknown_profile_is_not_found()
    {
        await Assert.ThrowsAsync<PersonNotFoundException>(() => Service().GetAsync(PersonId.New(), Alice, Cancellation));
    }

    [Fact]
    [Trait("AC", "AC-01.2")]
    public async Task One_account_lists_the_many_profiles_it_maintains_and_no_others()
    {
        var service = Service();
        await service.CreateAsync(new PersonName("Maya"), Alice, Cancellation);
        await service.CreateAsync(new PersonName("Gita"), Alice, Cancellation);
        await service.CreateAsync(new PersonName("Ram"), Bob, Cancellation);

        var list = await service.ListMaintainedAsync(Alice, Cancellation);

        Assert.Equal(["Gita", "Maya"], list.People.Select(person => person.Name.FirstName));
        Assert.False(list.IsTruncated);
    }

    [Fact]
    public async Task Maintained_list_stops_at_the_configured_limit_and_says_so()
    {
        var service = Service(maintainedListLimit: 1);
        await service.CreateAsync(new PersonName("Maya"), Alice, Cancellation);
        await service.CreateAsync(new PersonName("Gita"), Alice, Cancellation);

        var list = await service.ListMaintainedAsync(Alice, Cancellation);

        Assert.Single(list.People);
        Assert.True(list.IsTruncated);
    }

    [Fact]
    [Trait("AC", "AC-01.6")]
    public async Task Profiles_with_the_same_name_stay_separate()
    {
        var service = Service();

        var first = await service.CreateAsync(new PersonName("Maya", lastName: "Rai"), Alice, Cancellation);
        var second = await service.CreateAsync(new PersonName("Maya", lastName: "Rai"), Bob, Cancellation);

        Assert.NotEqual(first, second);
        Assert.True(_people.People[second].IsMaintainedBy(Bob));
    }
}
