using FamilyGraph.Business.Graph;
using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Business.Tests.Graph;

public class BansawaliLineTests
{
    [Fact]
    [Trait("AC", "AC-08.21")]
    public void Sons_line_continues_only_through_a_biological_son()
    {
        Assert.True(BansawaliLine.Sons.Continues(ParentChildType.Biological, Gender.Male));
    }

    [Theory]
    [Trait("AC", "AC-08.22")]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(null)]
    public void Sons_line_stops_at_a_daughter_and_at_a_gender_that_is_other_not_set_or_hidden(int? gender)
    {
        Assert.False(BansawaliLine.Sons.Continues(
            ParentChildType.Biological, gender is { } value ? Gender.FromValue(value) : null));
    }

    [Fact]
    [Trait("AC", "AC-08.22")]
    public void Sons_line_stops_at_an_adopted_son()
    {
        Assert.False(BansawaliLine.Sons.Continues(ParentChildType.Adoptive, Gender.Male));
    }

    [Theory]
    [Trait("AC", "AC-08.21")]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    [InlineData(2, null)]
    public void All_children_line_continues_through_every_child(int type, int? gender)
    {
        Assert.True(BansawaliLine.AllChildren.Continues(
            ParentChildType.FromValue(type), gender is { } value ? Gender.FromValue(value) : null));
    }
}
