using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Business.Tests.Import;

/// <summary>Profiles, links and marriages restored from an export file (D-033).</summary>
public class ImportedEntityTests
{
    private static readonly AccountId Alice = new(Guid.NewGuid());
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("AC", "AC-09.8")]
    public void An_imported_profile_keeps_its_id_name_visibility_gender_and_maintainer_and_records_no_event()
    {
        var id = PersonId.New();

        var person = Person.Import(id, new PersonName("Maya", "K", "Sharma"), ProfileVisibility.Private, null, Alice, Gender.Female);

        Assert.Equal(
            (id, new PersonName("Maya", "K", "Sharma"), ProfileVisibility.Private, Alice, Gender.Female, false),
            (person.Id, person.Name, person.Visibility, person.MaintainerId, person.Gender, person.IsDeleted));
        Assert.Empty(person.PendingEvents);
    }

    [Fact]
    [Trait("AC", "AC-09.8")]
    public void An_imported_deleted_profile_has_only_the_erased_name_and_no_gender()
    {
        var person = Person.Import(PersonId.New(), new PersonName("Ignored"), ProfileVisibility.Public, Now, Alice, Gender.Male);

        Assert.Equal((PersonName.Erased, null, Now), (person.Name, person.Gender, person.DeletedAt));
    }

    [Fact]
    [Trait("AC", "AC-09.8")]
    public void An_imported_link_keeps_both_approvals_and_the_birth_order_and_records_no_event()
    {
        var (id, parent, child) = (LinkId.New(), PersonId.New(), PersonId.New());

        var link = ParentChildLink.Import(
            id, parent, child, ParentChildType.Adoptive, EndpointApproval.Disagreed, EndpointApproval.Pending, 2);

        Assert.Equal(
            (id, parent, child, ParentChildType.Adoptive, EndpointApproval.Disagreed, EndpointApproval.Pending, (int?)2, false),
            (link.Id, link.ParentId, link.ChildId, link.Type, link.ParentApproval, link.ChildApproval, link.BirthOrder, link.IsActive));
        Assert.Empty(link.PendingEvents);
    }

    [Fact]
    [Trait("AC", "AC-09.11")]
    public void An_imported_link_cannot_be_to_oneself_or_have_a_birth_order_below_one()
    {
        var person = PersonId.New();

        Assert.Throws<SelfLinkException>(() => ParentChildLink.Import(
            LinkId.New(), person, person, ParentChildType.Biological, EndpointApproval.Approved, EndpointApproval.Approved, null));
        Assert.Throws<InvalidBirthOrderException>(() => ParentChildLink.Import(
            LinkId.New(), person, PersonId.New(), ParentChildType.Biological, EndpointApproval.Approved, EndpointApproval.Approved, 0));
    }

    [Fact]
    [Trait("AC", "AC-09.8")]
    public void An_imported_marriage_stores_the_spouses_in_id_order_with_their_own_approvals()
    {
        var (low, high) = (new PersonId(Guid.Parse("00000000-0000-0000-0000-000000000001")), new PersonId(Guid.Parse("00000000-0000-0000-0000-000000000002")));

        var marriage = Marriage.Import(LinkId.New(), high, low, EndpointApproval.Disagreed, EndpointApproval.Approved, isFormer: true);

        Assert.Equal(
            (low, high, EndpointApproval.Approved, EndpointApproval.Disagreed, true),
            (marriage.FirstSpouseId, marriage.SecondSpouseId, marriage.FirstApproval, marriage.SecondApproval, marriage.IsFormer));
        Assert.Empty(marriage.PendingEvents);
        Assert.Throws<SelfMarriageException>(() =>
            Marriage.Import(LinkId.New(), low, low, EndpointApproval.Approved, EndpointApproval.Approved, isFormer: false));
    }

    [Fact]
    [Trait("AC", "AC-09.11")]
    public void A_set_of_links_has_a_cycle_only_when_someone_becomes_their_own_ancestor()
    {
        var (a, b, c, d) = (PersonId.New(), PersonId.New(), PersonId.New(), PersonId.New());

        Assert.False(LinkCycleCheck.HasCycle([(a, b), (b, c), (a, c), (d, c)]));
        Assert.True(LinkCycleCheck.HasCycle([(a, b), (b, c), (c, a), (d, a)]));
        Assert.True(LinkCycleCheck.HasCycle([(a, a)]));
        Assert.False(LinkCycleCheck.HasCycle([]));
    }
}
