using FamilyGraph.Application.Privacy;
using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Tests.Privacy;

public class PrivacyProjectionTests
{
    private static readonly AccountId Alice = new(Guid.NewGuid());
    private static readonly PersonName Name = new("Maya", "Devi", "Gurung");

    [Fact]
    public void Public_profile_shows_its_name_to_any_signed_in_viewer()
    {
        var person = PrivacyProjection.Project(Record(ProfileVisibility.Public, maintainedByViewer: false), new Viewer(Alice, false));

        Assert.Equal((Name, null, ProfileVisibility.Public), (person.Name, person.Placeholder, person.Visibility));
    }

    [Fact]
    [Trait("AC", "AC-05.1")]
    public void Private_profile_is_a_placeholder_without_name_to_other_viewers()
    {
        var record = Record(ProfileVisibility.Private, maintainedByViewer: false);

        var person = PrivacyProjection.Project(record, new Viewer(Alice, false));

        Assert.Equal(
            (record.Id, null, PersonPlaceholder.Private, ProfileVisibility.Private, false),
            (person.Id, person.Name, person.Placeholder, person.Visibility, person.MaintainedByViewer));
    }

    [Fact]
    [Trait("AC", "AC-05.8")]
    public void Maintainer_sees_their_private_profile_by_name()
    {
        var person = PrivacyProjection.Project(Record(ProfileVisibility.Private, maintainedByViewer: true), new Viewer(Alice, false));

        Assert.Equal((Name, null, true), (person.Name, person.Placeholder, person.MaintainedByViewer));
    }

    [Fact]
    [Trait("AC", "AC-05.8")]
    public void Administrator_sees_a_private_profile_by_name()
    {
        var person = PrivacyProjection.Project(Record(ProfileVisibility.Private, maintainedByViewer: false), new Viewer(Alice, true));

        Assert.Equal((Name, null, false), (person.Name, person.Placeholder, person.MaintainedByViewer));
    }

    [Fact]
    [Trait("AC", "AC-05.3")]
    public void Deleted_profile_is_a_deleted_placeholder_to_everyone_including_its_maintainer_and_administrators()
    {
        var record = Record(ProfileVisibility.Public, maintainedByViewer: true, isDeleted: true);

        var asMaintainer = PrivacyProjection.Project(record, new Viewer(Alice, false));
        var asAdministrator = PrivacyProjection.Project(record, new Viewer(Alice, true));

        Assert.All([asMaintainer, asAdministrator], person =>
            Assert.Equal((record.Id, null, PersonPlaceholder.Deleted, false), (person.Id, person.Name, person.Placeholder, person.MaintainedByViewer)));
    }

    [Fact]
    [Trait("AC", "AC-05.13")]
    public void Placeholder_never_carries_the_image_but_a_visible_profile_does()
    {
        var image = ProfileImageId.New();

        var visible = PrivacyProjection.Project(Record(ProfileVisibility.Public, false, imageId: image), new Viewer(Alice, false));
        var hidden = PrivacyProjection.Project(Record(ProfileVisibility.Private, false, imageId: image), new Viewer(Alice, false));
        var deleted = PrivacyProjection.Project(Record(ProfileVisibility.Public, true, isDeleted: true, imageId: image), new Viewer(Alice, true));

        Assert.Equal((image, (ProfileImageId?)null, (ProfileImageId?)null), (visible.ImageId, hidden.ImageId, deleted.ImageId));
    }

    [Fact]
    [Trait("AC", "AC-05.14")]
    public void Gender_shows_with_the_name_and_never_on_a_placeholder()
    {
        var visible = PrivacyProjection.Project(Record(ProfileVisibility.Public, false), new Viewer(Alice, false));
        var hidden = PrivacyProjection.Project(Record(ProfileVisibility.Private, false), new Viewer(Alice, false));
        var deleted = PrivacyProjection.Project(Record(ProfileVisibility.Public, true, isDeleted: true), new Viewer(Alice, true));

        Assert.Equal((Gender.Female, (Gender?)null, (Gender?)null), (visible.Gender, hidden.Gender, deleted.Gender));
    }

    [Fact]
    [Trait("AC", "AC-05.15")]
    public void Description_shows_with_the_name_and_never_on_a_placeholder()
    {
        var visible = PrivacyProjection.Project(Record(ProfileVisibility.Public, false), new Viewer(Alice, false));
        var hidden = PrivacyProjection.Project(Record(ProfileVisibility.Private, false), new Viewer(Alice, false));
        var deleted = PrivacyProjection.Project(Record(ProfileVisibility.Public, true, isDeleted: true), new Viewer(Alice, true));

        Assert.Equal(("Teacher", (string?)null, (string?)null), (visible.Description, hidden.Description, deleted.Description));
    }

    [Fact]
    [Trait("AC", "AC-05.16")]
    public void A_birth_date_not_shown_to_others_is_seen_only_by_its_maintainer_and_administrators()
    {
        var others = PrivacyProjection.Project(Record(ProfileVisibility.Public, false), new Viewer(Alice, false));
        var maintainer = PrivacyProjection.Project(Record(ProfileVisibility.Public, true), new Viewer(Alice, false));
        var administrator = PrivacyProjection.Project(Record(ProfileVisibility.Public, false), new Viewer(Alice, true));

        Assert.Equal(
            ((BirthDate?)null, BirthDate.Ad(1952), BirthDate.Ad(1952)),
            (others.BirthDate, maintainer.BirthDate, administrator.BirthDate));
    }

    [Fact]
    [Trait("AC", "AC-05.16")]
    public void A_birth_date_shown_to_others_is_seen_by_everyone_who_sees_the_name_but_never_on_a_placeholder()
    {
        var visible = PrivacyProjection.Project(Record(ProfileVisibility.Public, false, birthDateShown: true), new Viewer(Alice, false));
        var hidden = PrivacyProjection.Project(Record(ProfileVisibility.Private, false, birthDateShown: true), new Viewer(Alice, false));
        var deleted = PrivacyProjection.Project(
            Record(ProfileVisibility.Public, true, isDeleted: true, birthDateShown: true), new Viewer(Alice, true));

        Assert.Equal((BirthDate.Ad(1952), true), (visible.BirthDate, visible.BirthDateShown));
        Assert.Equal(((BirthDate?)null, false), (hidden.BirthDate, hidden.BirthDateShown));
        Assert.Equal(((BirthDate?)null, false), (deleted.BirthDate, deleted.BirthDateShown));
    }

    private static PersonRecord Record(
        ProfileVisibility visibility,
        bool maintainedByViewer,
        bool isDeleted = false,
        ProfileImageId? imageId = null,
        bool birthDateShown = false) =>
        new(PersonId.New(), Name, visibility, isDeleted, maintainedByViewer, imageId, Gender.Female, "Teacher", BirthDate.Ad(1952), birthDateShown);
}
