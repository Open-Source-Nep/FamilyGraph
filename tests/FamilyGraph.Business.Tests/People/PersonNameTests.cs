using FamilyGraph.Business.People;

namespace FamilyGraph.Business.Tests.People;

public class PersonNameTests
{
    [Fact]
    [Trait("AC", "AC-01.7")]
    public void First_name_alone_is_a_valid_name()
    {
        var name = new PersonName("Sita");

        Assert.Equal("Sita", name.FirstName);
        Assert.Null(name.MiddleName);
        Assert.Null(name.LastName);
    }

    [Fact]
    public void Middle_and_last_names_are_kept_when_given()
    {
        var name = new PersonName("Ram", "Bahadur", "Thapa");

        Assert.Equal("Bahadur", name.MiddleName);
        Assert.Equal("Thapa", name.LastName);
    }

    [Theory]
    [Trait("AC", "AC-01.7")]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_first_name_is_rejected(string firstName)
    {
        var ex = Assert.Throws<FirstNameRequiredException>(() => new PersonName(firstName));

        Assert.Equal("person.first_name_required", ex.Code);
    }

    [Fact]
    public void Parts_are_trimmed_and_blank_optional_parts_are_absent()
    {
        var name = new PersonName("  Gita ", " ", "\tRai ");

        Assert.Equal("Gita", name.FirstName);
        Assert.Null(name.MiddleName);
        Assert.Equal("Rai", name.LastName);
    }
}
