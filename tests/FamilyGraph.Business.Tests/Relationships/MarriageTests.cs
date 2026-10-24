using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Business.Tests.Relationships;

public class MarriageTests
{
    private static readonly AccountId Alice = new(Guid.NewGuid());
    private static readonly AccountId Bob = new(Guid.NewGuid());
    private static readonly AccountId Carol = new(Guid.NewGuid());

    private static readonly DateTimeOffset Now = new(2026, 10, 24, 6, 0, 0, TimeSpan.Zero);

    private static Person NewPerson(string firstName, AccountId maintainer) =>
        Person.Create(new PersonName(firstName), maintainer);

    /// <summary>Alice maintains Ram, Bob maintains Sita, and both have approved.</summary>
    private static (Person Ram, Person Sita, Marriage Marriage) ActiveMarriageBetweenTwoMaintainers()
    {
        var ram = NewPerson("Ram", Alice);
        var sita = NewPerson("Sita", Bob);
        var marriage = Marriage.Create(ram, sita, Alice, alreadyMarried: false, directlyRelated: false);
        marriage.Approve(sita, Bob);
        marriage.TakeEvents();
        return (ram, sita, marriage);
    }

    [Fact]
    [Trait("AC", "AC-02.11")]
    public void A_marriage_between_two_profiles_of_one_maintainer_is_active_at_once()
    {
        var ram = NewPerson("Ram", Alice);
        var sita = NewPerson("Sita", Alice);

        var marriage = Marriage.Create(ram, sita, Alice, alreadyMarried: false, directlyRelated: false);

        Assert.True(marriage.IsActive);
        Assert.False(marriage.IsFormer);
        var created = Assert.IsType<MarriageCreated>(Assert.Single(marriage.PendingEvents));
        Assert.Equal(EndpointApproval.Approved, created.FirstApproval);
        Assert.Equal(EndpointApproval.Approved, created.SecondApproval);
    }

    [Fact]
    [Trait("AC", "AC-02.11")]
    public void The_other_maintainers_side_starts_pending_until_approved()
    {
        var ram = NewPerson("Ram", Alice);
        var sita = NewPerson("Sita", Bob);

        var marriage = Marriage.Create(ram, sita, Alice, alreadyMarried: false, directlyRelated: false);

        Assert.Equal(EndpointApproval.Approved, marriage.ApprovalOf(ram.Id));
        Assert.Equal(EndpointApproval.Pending, marriage.ApprovalOf(sita.Id));
        Assert.False(marriage.IsActive);

        marriage.Approve(sita, Bob);

        Assert.True(marriage.IsActive);
        Assert.IsType<MarriageSpouseApproved>(marriage.PendingEvents[^1]);
    }

    [Fact]
    public void Spouses_are_stored_in_ID_order_whichever_is_given_first()
    {
        var ram = NewPerson("Ram", Alice);
        var sita = NewPerson("Sita", Alice);

        var one = Marriage.Create(ram, sita, Alice, alreadyMarried: false, directlyRelated: false);
        var other = Marriage.Create(sita, ram, Alice, alreadyMarried: false, directlyRelated: false);

        Assert.Equal((one.FirstSpouseId, one.SecondSpouseId), (other.FirstSpouseId, other.SecondSpouseId));
        Assert.True(one.FirstSpouseId.Value.CompareTo(one.SecondSpouseId.Value) < 0);
        Assert.Equal(sita.Id, one.OtherSpouseOf(ram.Id));
    }

    [Fact]
    public void Only_a_maintainer_of_a_spouse_can_create_a_marriage()
    {
        var ram = NewPerson("Ram", Alice);
        var sita = NewPerson("Sita", Bob);

        Assert.Throws<NotSpouseMaintainerException>(() => Marriage.Create(ram, sita, Carol, alreadyMarried: false, directlyRelated: false));
    }

    [Fact]
    [Trait("AC", "AC-02.13")]
    public void A_person_cannot_marry_themselves()
    {
        var ram = NewPerson("Ram", Alice);

        Assert.Throws<SelfMarriageException>(() => Marriage.Create(ram, ram, Alice, alreadyMarried: false, directlyRelated: false));
    }

    [Fact]
    [Trait("AC", "AC-02.13")]
    public void A_pair_has_only_one_marriage()
    {
        var ram = NewPerson("Ram", Alice);
        var sita = NewPerson("Sita", Alice);

        Assert.Throws<DuplicateMarriageException>(() => Marriage.Create(ram, sita, Alice, alreadyMarried: true, directlyRelated: false));
    }

    [Fact]
    [Trait("AC", "AC-02.13")]
    public void A_person_can_have_several_marriages()
    {
        var ram = NewPerson("Ram", Alice);

        var first = Marriage.Create(ram, NewPerson("Sita", Alice), Alice, alreadyMarried: false, directlyRelated: false);
        var second = Marriage.Create(ram, NewPerson("Gita", Alice), Alice, alreadyMarried: false, directlyRelated: false);

        Assert.True(first.IsActive);
        Assert.True(second.IsActive);
    }

    [Fact]
    [Trait("AC", "AC-02.16")]
    public void A_parent_and_their_child_cannot_marry()
    {
        var hari = NewPerson("Hari", Alice);
        var maya = NewPerson("Maya", Alice);

        Assert.Throws<ParentChildMarriageException>(
            () => Marriage.Create(hari, maya, Alice, alreadyMarried: false, directlyRelated: true));
    }

    [Fact]
    [Trait("AC", "AC-02.16")]
    public void Spouses_cannot_be_linked_as_parent_and_child()
    {
        var ram = NewPerson("Ram", Alice);
        var sita = NewPerson("Sita", Alice);

        Assert.Throws<LinkBetweenSpousesException>(() => ParentChildLink.Create(
            ram,
            sita,
            ParentChildType.Biological,
            Alice,
            new HashSet<PersonId>(),
            new AncestorSnapshot(ram.Id, new HashSet<PersonId>(), isComplete: true),
            married: true));
    }

    [Fact]
    [Trait("AC", "AC-05.12")]
    public void No_marriage_with_a_deleted_profile()
    {
        var ram = NewPerson("Ram", Alice);
        var sita = NewPerson("Sita", Alice);
        sita.Delete(Alice, actorIsAdministrator: false, Now);

        Assert.Throws<MarriageToDeletedProfileException>(() => Marriage.Create(ram, sita, Alice, alreadyMarried: false, directlyRelated: false));
    }

    [Fact]
    [Trait("AC", "AC-02.12")]
    [Trait("AC", "AC-03.1")]
    public void Disagreement_deactivates_the_marriage_and_restoring_reactivates_it()
    {
        var (ram, sita, marriage) = ActiveMarriageBetweenTwoMaintainers();

        marriage.Disagree(sita, ram, Bob);

        Assert.False(marriage.IsActive);
        Assert.IsType<MarriageSpouseDisagreed>(marriage.PendingEvents[^1]);

        marriage.Approve(sita, Bob);

        Assert.True(marriage.IsActive);
        Assert.IsType<MarriageApprovalRestored>(marriage.PendingEvents[^1]);
    }

    [Fact]
    [Trait("AC", "AC-02.12")]
    public void A_pending_side_can_decline()
    {
        var ram = NewPerson("Ram", Alice);
        var sita = NewPerson("Sita", Bob);
        var marriage = Marriage.Create(ram, sita, Alice, alreadyMarried: false, directlyRelated: false);

        marriage.Disagree(sita, ram, Bob);

        Assert.Equal(EndpointApproval.Disagreed, marriage.ApprovalOf(sita.Id));
        Assert.IsType<MarriageSpouseDeclined>(marriage.PendingEvents[^1]);
    }

    [Fact]
    [Trait("AC", "AC-03.5")]
    public void Only_the_spouses_own_maintainer_changes_their_approval()
    {
        var (ram, sita, marriage) = ActiveMarriageBetweenTwoMaintainers();

        Assert.Throws<MarriageConsentNotMaintainerException>(() => marriage.Disagree(sita, ram, Alice));
        Assert.Throws<MarriageConsentNotMaintainerException>(() => marriage.Approve(sita, Carol));
    }

    [Fact]
    public void Repeating_an_approval_is_rejected()
    {
        var (_, sita, marriage) = ActiveMarriageBetweenTwoMaintainers();

        Assert.Throws<MarriageConsentUnchangedException>(() => marriage.Approve(sita, Bob));
    }

    [Fact]
    [Trait("AC", "AC-02.12")]
    public void A_sole_maintainer_deletes_instead_of_disagreeing()
    {
        var ram = NewPerson("Ram", Alice);
        var sita = NewPerson("Sita", Alice);
        var marriage = Marriage.Create(ram, sita, Alice, alreadyMarried: false, directlyRelated: false);

        Assert.Throws<MarriageSoleMaintainerMustDeleteException>(() => marriage.Disagree(sita, ram, Alice));

        marriage.Delete(ram, sita, Alice, Now);

        Assert.True(marriage.IsDeleted);
        Assert.False(marriage.IsActive);
        Assert.IsType<MarriageDeleted>(marriage.PendingEvents[^1]);
        Assert.Throws<MarriageDeletedException>(() => marriage.SetFormer(ram, sita, Alice, isFormer: true));
    }

    [Fact]
    [Trait("AC", "AC-02.12")]
    public void Different_maintainers_cannot_delete()
    {
        var (ram, sita, marriage) = ActiveMarriageBetweenTwoMaintainers();

        Assert.Throws<MarriageNotSoleMaintainerException>(() => marriage.Delete(ram, sita, Alice, Now));
    }

    [Fact]
    [Trait("AC", "AC-02.14")]
    public void Either_spouses_maintainer_marks_the_marriage_former_or_current_without_consent()
    {
        var (ram, sita, marriage) = ActiveMarriageBetweenTwoMaintainers();

        marriage.SetFormer(sita, ram, Bob, isFormer: true);

        Assert.True(marriage.IsFormer);
        Assert.True(marriage.IsActive);
        var changed = Assert.IsType<MarriageFormerChanged>(marriage.PendingEvents[^1]);
        Assert.True(changed.IsFormer);

        marriage.SetFormer(ram, sita, Alice, isFormer: false);

        Assert.False(marriage.IsFormer);
        Assert.Equal(EndpointApproval.Approved, marriage.ApprovalOf(ram.Id));
        Assert.Equal(EndpointApproval.Approved, marriage.ApprovalOf(sita.Id));
    }

    [Fact]
    [Trait("AC", "AC-02.14")]
    public void Marking_former_never_activates_an_inactive_marriage()
    {
        var ram = NewPerson("Ram", Alice);
        var sita = NewPerson("Sita", Bob);
        var marriage = Marriage.Create(ram, sita, Alice, alreadyMarried: false, directlyRelated: false);

        marriage.SetFormer(ram, sita, Alice, isFormer: true);

        Assert.True(marriage.IsFormer);
        Assert.False(marriage.IsActive);
    }

    [Fact]
    [Trait("AC", "AC-02.14")]
    public void Someone_who_maintains_neither_spouse_cannot_mark_it_former()
    {
        var (ram, sita, marriage) = ActiveMarriageBetweenTwoMaintainers();

        Assert.Throws<NotSpouseMaintainerException>(() => marriage.SetFormer(ram, sita, Carol, isFormer: true));
    }

    [Fact]
    public void Marking_it_the_way_it_already_is_is_rejected()
    {
        var (ram, sita, marriage) = ActiveMarriageBetweenTwoMaintainers();

        Assert.Throws<MarriageFormerUnchangedException>(() => marriage.SetFormer(ram, sita, Alice, isFormer: false));
    }

    [Fact]
    [Trait("AC", "AC-02.14")]
    public void The_maintainer_of_a_deleted_spouse_cannot_mark_it_former()
    {
        var (ram, sita, marriage) = ActiveMarriageBetweenTwoMaintainers();
        sita.Delete(Bob, actorIsAdministrator: false, Now);

        Assert.Throws<NotSpouseMaintainerException>(() => marriage.SetFormer(sita, ram, Bob, isFormer: true));

        marriage.SetFormer(ram, sita, Alice, isFormer: true);
        Assert.True(marriage.IsFormer);
    }

    [Fact]
    [Trait("AC", "AC-07.1")]
    public void Marriage_events_hold_no_personal_data()
    {
        var (ram, sita, marriage) = ActiveMarriageBetweenTwoMaintainers();
        marriage.SetFormer(ram, sita, Alice, isFormer: true);

        var changed = Assert.IsType<MarriageFormerChanged>(Assert.Single(marriage.TakeEvents()));

        Assert.Equal(marriage.Id, changed.MarriageId);
        Assert.Equal(Alice, changed.ActorId);
    }
}
