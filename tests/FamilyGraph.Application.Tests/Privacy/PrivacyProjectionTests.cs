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

    private static PersonRecord Record(ProfileVisibility visibility, bool maintainedByViewer, bool isDeleted = false) =>
        new(PersonId.New(), Name, visibility, isDeleted, maintainedByViewer);
}
