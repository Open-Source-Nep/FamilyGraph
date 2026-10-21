namespace FamilyGraph.ArchitectureTests;

/// <summary>Enforces the project reference graph agreed in D-002 and the test layout agreed in D-004.</summary>
public class ProjectReferenceTests
{
    private const string Business = "FamilyGraph.Business";
    private const string Application = "FamilyGraph.Application";
    private const string Infrastructure = "FamilyGraph.Infrastructure";
    private const string Server = "Server";

    public static TheoryData<string, string[]> AllowedSourceReferences => new()
    {
        { Server, [Application, Infrastructure] },
        { Application, [Business] },
        { Infrastructure, [Business, Application] },
        { Business, [] },
    };

    [Theory]
    [MemberData(nameof(AllowedSourceReferences))]
    public void Source_project_references_only_what_D002_allows(string project, string[] allowed)
    {
        var references = RepositoryLayout.ProjectReferences(
            RepositoryLayout.Load(RepositoryLayout.ProjectFile("src", project)));

        Assert.Empty(references.Except(allowed));
    }

    [Fact]
    public void Business_has_no_package_or_framework_references()
    {
        var project = RepositoryLayout.Load(RepositoryLayout.ProjectFile("src", Business));

        Assert.Equal("Microsoft.NET.Sdk", (string?)project.Root!.Attribute("Sdk"));
        Assert.Empty(project.Descendants("PackageReference"));
        Assert.Empty(project.Descendants("FrameworkReference"));
    }

    [Theory]
    [InlineData(Business)]
    [InlineData(Application)]
    [InlineData(Infrastructure)]
    public void Unit_test_project_references_only_its_project_under_test(string project)
    {
        var references = RepositoryLayout.ProjectReferences(
            RepositoryLayout.Load(RepositoryLayout.ProjectFile("tests", project + ".Tests")));

        Assert.Equal([project], references);
    }
}
