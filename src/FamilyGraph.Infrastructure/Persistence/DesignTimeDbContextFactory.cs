using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FamilyGraph.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> build the context to add migrations or a migration bundle. Adding a
/// migration does not connect; commands that do take <c>--connection</c>.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FamilyGraphDbContext>
{
    public FamilyGraphDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FamilyGraphDbContext>();
        options.UseFamilyGraphNpgsql("Host=localhost;Database=familygraph");
        return new FamilyGraphDbContext(options.Options);
    }
}
