using FamilyGraph.Business.Relationships;

namespace FamilyGraph.Business.Tests;

public class BaseEnumTests
{
    [Fact]
    public void All_lists_every_declared_value_in_order()
    {
        Assert.Equal([ParentChildType.Biological, ParentChildType.Adoptive], ParentChildType.All);
    }

    [Fact]
    public void Value_and_name_round_trip_to_the_same_instance()
    {
        foreach (var type in ParentChildType.All)
        {
            Assert.Same(type, ParentChildType.FromValue(type.Value));
            Assert.Same(type, ParentChildType.FromName(type.Name));
        }
    }

    [Fact]
    public void Unknown_value_or_name_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ParentChildType.FromValue(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ParentChildType.FromName("Step"));
    }

    [Fact]
    public void Values_compare_by_value_and_print_their_name()
    {
        Assert.True(ParentChildType.Biological == ParentChildType.FromValue(1));
        Assert.True(ParentChildType.Biological != ParentChildType.Adoptive);
        Assert.False(ParentChildType.Biological == null);
        Assert.Equal("Adoptive", ParentChildType.Adoptive.ToString());
    }
}
