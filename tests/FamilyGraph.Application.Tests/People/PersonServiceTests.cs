using FamilyGraph.Application.People;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Tests.People;

public class PersonServiceTests
{
    private static readonly AccountId Alice = new(Guid.NewGuid());
    private static readonly AccountId Bob = new(Guid.NewGuid());
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 9, 30, 0, TimeSpan.Zero);

    private readonly InMemoryPeople _people = new();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static Viewer As(AccountId account) => new(account, IsAdministrator: false);

    private PersonService Service(
        int maintainedListLimit = PeopleLimits.DefaultMaintainedListLimit, int searchLimit = PeopleLimits.DefaultSearchLimit) =>
        new(_people, _people, _people, _people, _people, new FixedTimeProvider(Now), new PeopleLimits(maintainedListLimit, searchLimit));

    [Fact]
    [Trait("AC", "AC-01.1")]
    [Trait("AC", "AC-01.7")]
    public async Task Create_saves_a_profile_with_only_a_first_name_maintained_by_the_creator()
    {
        var id = await Service().CreateAsync(new PersonName("Maya"), null, Alice, Cancellation);

        var person = _people.People[id];
        Assert.Equal(new PersonName("Maya"), person.Name);
        Assert.True(person.IsMaintainedBy(Alice));
        Assert.Equal(1, _people.SaveCount);
    }

    [Fact]
    public async Task Create_saves_the_creation_audit_event_with_the_time()
    {
        var id = await Service().CreateAsync(new PersonName("Maya"), null, Alice, Cancellation);

        var (auditEvent, occurredAt) = Assert.Single(_people.AuditEvents);
        Assert.Equal(new PersonCreated(id, Alice), auditEvent);
        Assert.Equal(Now, occurredAt);
    }

    [Fact]
    [Trait("AC", "AC-01.8")]
    public async Task Maintainer_renames_the_profile_and_the_rename_is_audited()
    {
        var service = Service();
        var id = await service.CreateAsync(new PersonName("Maya"), null, Alice, Cancellation);

        await service.RenameAsync(id, new PersonName("Maya", "Devi", "Rai"), Alice, Cancellation);

        Assert.Equal(new PersonName("Maya", "Devi", "Rai"), _people.People[id].Name);
        Assert.Equal(new PersonRenamed(id, Alice), _people.AuditEvents[^1].Event);
    }

    [Fact]
    [Trait("AC", "AC-01.8")]
    public async Task Rename_by_someone_else_is_refused_and_nothing_is_saved()
    {
        var service = Service();
        var id = await service.CreateAsync(new PersonName("Maya"), null, Alice, Cancellation);

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
        var id = await service.CreateAsync(new PersonName("Maya"), null, Alice, Cancellation);

        var asMaintainer = await service.GetAsync(id, As(Alice), Cancellation);
        var asOther = await service.GetAsync(id, As(Bob), Cancellation);

        Assert.True(asMaintainer.MaintainedByViewer);
        Assert.False(asOther.MaintainedByViewer);
        Assert.Equal(new PersonName("Maya"), asOther.Name);
    }

    [Fact]
    [Trait("AC", "AC-01.21")]
    public async Task A_person_is_created_with_a_gender()
    {
        var service = Service();

        var id = await service.CreateAsync(new PersonName("Maya"), Gender.Female, Alice, Cancellation);

        Assert.Equal(Gender.Female, (await service.GetAsync(id, new Viewer(Alice, false), Cancellation)).Gender);
    }

    [Fact]
    [Trait("AC", "AC-01.22")]
    public async Task Maintainer_sets_the_gender_and_the_change_is_audited()
    {
        var service = Service();
        var id = await service.CreateAsync(new PersonName("Maya"), null, Alice, Cancellation);

        await service.SetGenderAsync(id, Gender.Female, Alice, Cancellation);

        Assert.Equal(Gender.Female, _people.People[id].Gender);
        Assert.Equal((new PersonGenderChanged(id, Alice), Now), _people.AuditEvents[^1]);
        Assert.Equal(2, _people.SaveCount);
    }

    [Fact]
    [Trait("AC", "AC-01.22")]
    public async Task Someone_other_than_the_maintainer_cannot_set_the_gender()
    {
        var service = Service();
        var id = await service.CreateAsync(new PersonName("Maya"), null, Alice, Cancellation);

        await Assert.ThrowsAsync<NotProfileMaintainerException>(() => service.SetGenderAsync(id, Gender.Male, Bob, Cancellation));
        Assert.Null(_people.People[id].Gender);
        Assert.Equal(1, _people.SaveCount);
    }

    [Fact]
    [Trait("AC", "AC-05.7")]
    public async Task Maintainer_makes_a_profile_private_and_the_change_is_audited()
    {
        var service = Service();
        var id = await service.CreateAsync(new PersonName("Maya"), null, Alice, Cancellation);

        await service.SetVisibilityAsync(id, ProfileVisibility.Private, Alice, Cancellation);

        Assert.Equal(ProfileVisibility.Private, _people.People[id].Visibility);
        Assert.Equal((new PersonVisibilityChanged(id, Alice, ProfileVisibility.Private), Now), _people.AuditEvents[^1]);
        Assert.Equal(2, _people.SaveCount);
    }

    [Fact]
    public async Task Changing_the_visibility_of_an_unknown_profile_is_not_found()
    {
        await Assert.ThrowsAsync<PersonNotFoundException>(
            () => Service().SetVisibilityAsync(PersonId.New(), ProfileVisibility.Private, Alice, Cancellation));
    }

    [Fact]
    [Trait("AC", "AC-05.1")]
    [Trait("AC", "AC-05.8")]
    public async Task Private_profile_is_a_placeholder_to_others_but_not_to_its_maintainer_or_administrators()
    {
        var service = Service();
        var id = await service.CreateAsync(new PersonName("Maya"), null, Alice, Cancellation);
        await service.SetVisibilityAsync(id, ProfileVisibility.Private, Alice, Cancellation);

        var asOther = await service.GetAsync(id, As(Bob), Cancellation);
        var asMaintainer = await service.GetAsync(id, As(Alice), Cancellation);
        var asAdministrator = await service.GetAsync(id, new Viewer(Bob, IsAdministrator: true), Cancellation);

        Assert.Equal((null, PersonPlaceholder.Private), (asOther.Name, asOther.Placeholder));
        Assert.Equal(new PersonName("Maya"), asMaintainer.Name);
        Assert.Equal(new PersonName("Maya"), asAdministrator.Name);
    }

    [Fact]
    [Trait("AC", "AC-05.10")]
    [Trait("AC", "AC-05.11")]
    public async Task Maintainer_deletes_a_profile_which_leaves_a_deleted_placeholder_and_their_list()
    {
        var service = Service();
        var id = await service.CreateAsync(new PersonName("Maya", lastName: "Rai"), null, Alice, Cancellation);

        await service.DeleteAsync(id, As(Alice), Cancellation);

        Assert.Equal((new PersonDeleted(id, Alice, ProfileDeleter.Maintainer), Now), _people.AuditEvents[^1]);
        Assert.Equal(Now, _people.People[id].DeletedAt);
        var asMaintainer = await service.GetAsync(id, As(Alice), Cancellation);
        Assert.Equal((null, PersonPlaceholder.Deleted, false), (asMaintainer.Name, asMaintainer.Placeholder, asMaintainer.MaintainedByViewer));
        Assert.Empty((await service.ListMaintainedAsync(Alice, Cancellation)).People);
    }

    [Fact]
    [Trait("AC", "AC-05.11")]
    public async Task Administrator_deletes_another_accounts_profile_and_others_cannot()
    {
        var service = Service();
        var id = await service.CreateAsync(new PersonName("Maya"), null, Alice, Cancellation);

        await Assert.ThrowsAsync<NotProfileMaintainerException>(() => service.DeleteAsync(id, As(Bob), Cancellation));
        await service.DeleteAsync(id, new Viewer(Bob, IsAdministrator: true), Cancellation);

        Assert.Equal(new PersonDeleted(id, Bob, ProfileDeleter.Administrator), _people.AuditEvents[^1].Event);
    }

    [Fact]
    public async Task Deleting_an_unknown_profile_is_not_found()
    {
        await Assert.ThrowsAsync<PersonNotFoundException>(() => Service().DeleteAsync(PersonId.New(), As(Alice), Cancellation));
    }

    [Fact]
    public async Task Viewing_an_unknown_profile_is_not_found()
    {
        await Assert.ThrowsAsync<PersonNotFoundException>(() => Service().GetAsync(PersonId.New(), As(Alice), Cancellation));
    }

    [Fact]
    [Trait("AC", "AC-01.2")]
    public async Task One_account_lists_the_many_profiles_it_maintains_and_no_others()
    {
        var service = Service();
        await service.CreateAsync(new PersonName("Maya"), null, Alice, Cancellation);
        await service.CreateAsync(new PersonName("Gita"), null, Alice, Cancellation);
        await service.CreateAsync(new PersonName("Ram"), null, Bob, Cancellation);

        var list = await service.ListMaintainedAsync(Alice, Cancellation);

        Assert.Equal(["Gita", "Maya"], list.People.Select(person => person.Name.FirstName));
        Assert.False(list.IsTruncated);
    }

    [Fact]
    public async Task Maintained_list_stops_at_the_configured_limit_and_says_so()
    {
        var service = Service(maintainedListLimit: 1);
        await service.CreateAsync(new PersonName("Maya"), null, Alice, Cancellation);
        await service.CreateAsync(new PersonName("Gita"), null, Alice, Cancellation);

        var list = await service.ListMaintainedAsync(Alice, Cancellation);

        Assert.Single(list.People);
        Assert.True(list.IsTruncated);
    }

    [Fact]
    [Trait("AC", "AC-08.11")]
    public async Task Search_finds_public_profiles_of_every_maintainer_and_your_own_private_ones()
    {
        var service = Service();
        var bobsPublic = await service.CreateAsync(new PersonName("Maya", lastName: "Rai"), null, Bob, Cancellation);
        var alicesPrivate = await service.CreateAsync(new PersonName("Maya", lastName: "Gurung"), null, Alice, Cancellation);
        await service.SetVisibilityAsync(alicesPrivate, ProfileVisibility.Private, Alice, Cancellation);
        await service.CreateAsync(new PersonName("Gita"), null, Bob, Cancellation);

        var result = await service.SearchAsync("maya", As(Alice), Cancellation);

        Assert.Equal([alicesPrivate, bobsPublic], result.People.Select(person => person.Id));
        Assert.All(result.People, person => Assert.Null(person.Placeholder));
        Assert.Equal([true, false], result.People.Select(person => person.MaintainedByViewer));
        Assert.False(result.IsTruncated);
    }

    [Fact]
    [Trait("AC", "AC-05.1")]
    [Trait("AC", "AC-08.11")]
    public async Task Search_never_finds_a_private_or_deleted_profile_by_its_hidden_name()
    {
        var service = Service();
        var hidden = await service.CreateAsync(new PersonName("Maya"), null, Bob, Cancellation);
        await service.SetVisibilityAsync(hidden, ProfileVisibility.Private, Bob, Cancellation);
        var deleted = await service.CreateAsync(new PersonName("Maya"), null, Alice, Cancellation);
        await service.DeleteAsync(deleted, As(Alice), Cancellation);

        Assert.Empty((await service.SearchAsync("Maya", As(Alice), Cancellation)).People);
        Assert.Empty((await service.SearchAsync("erased", As(Alice), Cancellation)).People);
    }

    [Fact]
    [Trait("AC", "AC-08.11")]
    public async Task Every_word_of_the_search_must_appear_in_a_name_part()
    {
        var service = Service();
        var mayaRai = await service.CreateAsync(new PersonName("Maya", "Devi", "Rai"), null, Bob, Cancellation);
        await service.CreateAsync(new PersonName("Maya", lastName: "Gurung"), null, Bob, Cancellation);

        var result = await service.SearchAsync("  rai   MAY ", As(Alice), Cancellation);

        Assert.Equal(mayaRai, Assert.Single(result.People).Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Blank_search_finds_nobody(string? text)
    {
        var service = Service();
        await service.CreateAsync(new PersonName("Maya"), null, Bob, Cancellation);

        var result = await service.SearchAsync(text, As(Alice), Cancellation);

        Assert.Empty(result.People);
        Assert.False(result.IsTruncated);
    }

    [Fact]
    public async Task Search_stops_at_the_configured_limit_and_says_so()
    {
        var service = Service(searchLimit: 1);
        await service.CreateAsync(new PersonName("Maya"), null, Bob, Cancellation);
        await service.CreateAsync(new PersonName("Mayank"), null, Bob, Cancellation);

        var result = await service.SearchAsync("may", As(Alice), Cancellation);

        Assert.Single(result.People);
        Assert.True(result.IsTruncated);
    }

    [Fact]
    [Trait("AC", "AC-01.6")]
    public async Task Profiles_with_the_same_name_stay_separate()
    {
        var service = Service();

        var first = await service.CreateAsync(new PersonName("Maya", lastName: "Rai"), null, Alice, Cancellation);
        var second = await service.CreateAsync(new PersonName("Maya", lastName: "Rai"), null, Bob, Cancellation);

        Assert.NotEqual(first, second);
        Assert.True(_people.People[second].IsMaintainedBy(Bob));
    }

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    [Trait("AC", "AC-01.18")]
    public async Task Maintainer_sets_an_image_which_viewers_of_the_profile_can_load()
    {
        var service = Service();
        var id = await service.CreateAsync(new PersonName("Maya"), null, Alice, Cancellation);

        var imageId = await service.SetImageAsync(id, Jpeg, Alice, Cancellation);

        Assert.Equal(imageId, (await service.GetAsync(id, As(Bob), Cancellation)).ImageId);
        var image = await service.GetImageAsync(id, As(Bob), Cancellation);
        Assert.Equal((imageId, ImageFormat.Jpeg, Jpeg), (image.Id, image.Format, image.Content));
        Assert.Equal((new PersonImageChanged(id, Alice), Now), _people.AuditEvents[^1]);
    }

    [Fact]
    [Trait("AC", "AC-01.18")]
    public async Task Replacing_the_image_removes_the_old_one()
    {
        var service = Service();
        var id = await service.CreateAsync(new PersonName("Maya"), null, Alice, Cancellation);
        var first = await service.SetImageAsync(id, Jpeg, Alice, Cancellation);

        var second = await service.SetImageAsync(id, Png, Alice, Cancellation);

        Assert.Equal([second], _people.Images.Keys);
        Assert.NotEqual(first, second);
    }

    [Fact]
    [Trait("AC", "AC-01.18")]
    public async Task Removing_the_image_deletes_it()
    {
        var service = Service();
        var id = await service.CreateAsync(new PersonName("Maya"), null, Alice, Cancellation);
        await service.SetImageAsync(id, Jpeg, Alice, Cancellation);

        await service.RemoveImageAsync(id, Alice, Cancellation);

        Assert.Empty(_people.Images);
        Assert.Null(_people.People[id].ImageId);
        await Assert.ThrowsAsync<ProfileImageNotFoundException>(() => service.GetImageAsync(id, As(Alice), Cancellation));
    }

    [Fact]
    [Trait("AC", "AC-01.18")]
    public async Task Someone_else_cannot_set_the_image_and_nothing_is_stored()
    {
        var service = Service();
        var id = await service.CreateAsync(new PersonName("Maya"), null, Alice, Cancellation);

        await Assert.ThrowsAsync<NotProfileMaintainerException>(() => service.SetImageAsync(id, Jpeg, Bob, Cancellation));

        Assert.Empty(_people.Images);
    }

    [Fact]
    [Trait("AC", "AC-05.13")]
    public async Task A_private_profile_image_is_not_found_for_others_but_loads_for_its_maintainer()
    {
        var service = Service();
        var id = await service.CreateAsync(new PersonName("Maya"), null, Alice, Cancellation);
        await service.SetImageAsync(id, Jpeg, Alice, Cancellation);
        await service.SetVisibilityAsync(id, ProfileVisibility.Private, Alice, Cancellation);

        await Assert.ThrowsAsync<ProfileImageNotFoundException>(() => service.GetImageAsync(id, As(Bob), Cancellation));
        Assert.Null((await service.GetAsync(id, As(Bob), Cancellation)).ImageId);
        Assert.Equal(Jpeg, (await service.GetImageAsync(id, As(Alice), Cancellation)).Content);
    }

    [Fact]
    [Trait("AC", "AC-05.10")]
    [Trait("AC", "AC-05.13")]
    public async Task Deleting_the_profile_erases_its_image()
    {
        var service = Service();
        var id = await service.CreateAsync(new PersonName("Maya"), null, Alice, Cancellation);
        await service.SetImageAsync(id, Jpeg, Alice, Cancellation);

        await service.DeleteAsync(id, As(Alice), Cancellation);

        Assert.Empty(_people.Images);
        await Assert.ThrowsAsync<ProfileImageNotFoundException>(
            () => service.GetImageAsync(id, new Viewer(Bob, IsAdministrator: true), Cancellation));
    }
}
