using System.Xml.Linq;

namespace FamilyGraph.ArchitectureTests;

/// <summary>Locates the repository and reads project files, so tests can check the declared reference graph.</summary>
internal static class RepositoryLayout
{
    public static readonly DirectoryInfo Root = FindRoot();

    public static FileInfo ProjectFile(string folder, string projectName) =>
        new(Path.Combine(Root.FullName, folder, projectName, projectName + ".csproj"));

    public static XDocument Load(FileInfo projectFile)
    {
        Assert.True(projectFile.Exists, $"Project file not found: {projectFile.FullName}");
        return XDocument.Load(projectFile.FullName);
    }

    /// <summary>Names of the projects referenced via ProjectReference, e.g. "FamilyGraph.Business".</summary>
    public static IReadOnlySet<string> ProjectReferences(XDocument project) =>
        project.Descendants("ProjectReference")
            .Select(r => Path.GetFileNameWithoutExtension(((string)r.Attribute("Include")!).Replace('\\', '/')))
            .ToHashSet();

    private static DirectoryInfo FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "FamilyGraph.slnx")))
            {
                return dir;
            }
        }

        throw new InvalidOperationException("Could not find FamilyGraph.slnx above the test output directory.");
    }
}
