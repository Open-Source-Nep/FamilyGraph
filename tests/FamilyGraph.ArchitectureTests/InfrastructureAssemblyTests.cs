using System.Reflection;

namespace FamilyGraph.ArchitectureTests;

/// <summary>Keeps EF Core types inside Infrastructure (issue #9, D-002).</summary>
public class InfrastructureAssemblyTests
{
    [Fact]
    public void Infrastructure_exposes_no_DbContext_or_EF_types()
    {
        var infrastructure = Assembly.Load(new AssemblyName("FamilyGraph.Infrastructure"));

        // Generated migrations are public, but they only describe schema changes.
        var exposed = infrastructure.GetExportedTypes()
            .Where(type => type.Namespace != "FamilyGraph.Infrastructure.Persistence.Migrations")
            .Where(type => BaseTypes(type).Any(baseType => baseType.FullName == "Microsoft.EntityFrameworkCore.DbContext")
                || type.Namespace?.StartsWith("FamilyGraph.Infrastructure.Persistence", StringComparison.Ordinal) == true)
            .Select(type => type.FullName);

        Assert.Empty(exposed);
    }

    private static IEnumerable<Type> BaseTypes(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            yield return current;
        }
    }
}
