using System.Reflection;

namespace FamilyGraph.ArchitectureTests;

/// <summary>Checks the compiled Business assembly, which also catches dependencies that arrive transitively.</summary>
public class BusinessAssemblyTests
{
    private static readonly string[] ForbiddenPrefixes =
    [
        "Microsoft.AspNetCore",
        "Microsoft.EntityFrameworkCore",
        "FamilyGraph.Application",
        "FamilyGraph.Infrastructure",
        "Server",
    ];

    [Fact]
    public void Business_assembly_does_not_reference_AspNetCore_EfCore_or_other_layers()
    {
        var business = Assembly.Load(new AssemblyName("FamilyGraph.Business"));

        var forbidden = business.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(name => ForbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)));

        Assert.Empty(forbidden);
    }
}
