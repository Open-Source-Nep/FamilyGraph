using FamilyGraph.Business.Graph;
using FamilyGraph.Business.Kinship;
using FamilyGraph.Business.People;
using Server.Localization;

namespace Server.IntegrationTests.Localization;

/// <summary>The culture files shipped in <c>src/Server/Cultures</c> and the checks on loading them (D-048).</summary>
public sealed class CultureCatalogTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("cultures").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static string Shipped()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FamilyGraph.slnx")))
            {
                return Path.Combine(directory.FullName, "src", "Server", "Cultures");
            }
        }
        throw new InvalidOperationException("The repository root (FamilyGraph.slnx) was not found.");
    }

    private static string? Label(KinshipCulture culture, string language, Gender? start, params KinshipStepFacts[] steps) =>
        culture.Label(new KinshipFacts(start, steps)) is { } label ? culture.Text(label, language) : null;

    [Fact]
    [Trait("AC", "AC-08.28")]
    public void Ships_English_and_Nepali_with_their_languages()
    {
        var catalog = CultureCatalog.Load(Shipped(), "en", ["en", "ne"]);

        Assert.Equal(["en", "ne"], catalog.Available.Select(culture => culture.Code));
        Assert.Equal(["en"], catalog.FindOrDefault("en").Languages);
        Assert.Equal(["ne", "en"], catalog.FindOrDefault("ne").Languages);
        Assert.Equal("en", catalog.FindOrDefault("unknown").Code);
    }

    [Fact]
    [Trait("AC", "AC-08.30")]
    [Trait("AC", "AC-08.33")]
    public void Nepali_labels_tell_a_fathers_younger_brother_from_an_elder_one_in_both_languages()
    {
        var nepali = CultureCatalog.Load(Shipped(), "ne", ["en", "ne"]).FindOrDefault("ne");
        KinshipStepFacts Father() => new(StepRelation.Parent, Gender.Male);
        KinshipStepFacts Grandparent() => new(StepRelation.Parent, null);

        Assert.Equal("काका", Label(nepali, "ne", null, Father(), Grandparent(), new(StepRelation.Child, Gender.Male, RelativeAge.Younger)));
        Assert.Equal("Kaka", Label(nepali, "en", null, Father(), Grandparent(), new(StepRelation.Child, Gender.Male, RelativeAge.Younger)));
        Assert.Equal("Thulobuwa", Label(nepali, "en", null, Father(), Grandparent(), new(StepRelation.Child, Gender.Male, RelativeAge.Elder)));
        Assert.Equal("Thulobuwa/Kaka", Label(nepali, "en", null, Father(), Grandparent(), new(StepRelation.Child, Gender.Male)));
        Assert.Equal("Mama", Label(nepali, "en", null, new(StepRelation.Parent, Gender.Female), Grandparent(), new(StepRelation.Child, Gender.Male)));
    }

    [Fact]
    [Trait("AC", "AC-08.29")]
    public void Nepali_labels_depend_on_the_start_persons_gender()
    {
        var nepali = CultureCatalog.Load(Shipped(), "ne", ["en", "ne"]).FindOrDefault("ne");
        KinshipStepFacts[] brothersSon = [new(StepRelation.Parent, null), new(StepRelation.Child, Gender.Male), new(StepRelation.Child, Gender.Male)];

        Assert.Equal("Bhatija", Label(nepali, "en", Gender.Male, brothersSon));
        Assert.Equal("Bhatija", Label(nepali, "en", null, brothersSon));
        Assert.Equal("Bhada", Label(nepali, "en", Gender.Female, brothersSon));
    }

    [Fact]
    public void English_falls_back_to_relative()
    {
        var english = CultureCatalog.Load(Shipped(), "en", ["en", "ne"]).FindOrDefault("en");

        Assert.Equal("Relative", Label(english, "en", null, new(StepRelation.Spouse, null), new(StepRelation.Spouse, null)));
        Assert.Equal("Aunt", Label(english, "en", null, new(StepRelation.Parent, null), new(StepRelation.Parent, null), new(StepRelation.Child, Gender.Female)));
    }

    [Fact]
    public void A_default_culture_without_a_file_stops_the_startup()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => CultureCatalog.Load(Shipped(), "fr", ["en", "ne"]));
        Assert.Contains("Cultures:Default", exception.Message);
    }

    [Fact]
    public void A_culture_using_a_language_the_instance_does_not_offer_stops_the_startup()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => CultureCatalog.Load(Shipped(), "en", ["en"]));
        Assert.Contains("\"ne\"", exception.Message);
    }

    [Theory]
    [InlineData("parent:tall", "\"tall\"")]
    [InlineData("cousin", "\"cousin\"")]
    [InlineData("child:elder", "Elder or younger")]
    [InlineData("child:nth=0", "\"0\"")]
    [InlineData("parent:nth=1", "Only a child step")]
    public void A_rule_that_cannot_be_read_stops_the_startup_and_names_the_rule(string path, string expected)
    {
        File.WriteAllText(Path.Combine(_directory, "xx.json"), $$"""
            { "code": "xx", "languages": ["en"], "name": { "en": "X" }, "fallback": { "en": "Relative" },
              "rules": [ { "path": "parent", "label": { "en": "Parent" } }, { "path": "{{path}}", "label": { "en": "X" } } ] }
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => CultureCatalog.Load(_directory, "xx", ["en"]));

        Assert.Contains("Rule 2", exception.Message);
        Assert.Contains(expected, exception.Message);
    }

    [Fact]
    public void A_label_missing_one_of_the_cultures_languages_stops_the_startup()
    {
        File.WriteAllText(Path.Combine(_directory, "xx.json"), """
            { "code": "xx", "languages": ["en", "ne"], "name": { "en": "X", "ne": "X" }, "fallback": { "en": "R", "ne": "R" },
              "rules": [ { "path": "parent", "label": { "en": "Parent" } } ] }
            """);

        Assert.Throws<InvalidOperationException>(() => CultureCatalog.Load(_directory, "xx", ["en", "ne"]));
    }
}
