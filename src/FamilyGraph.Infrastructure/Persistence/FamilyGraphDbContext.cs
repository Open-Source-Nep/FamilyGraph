using Microsoft.EntityFrameworkCore;

namespace FamilyGraph.Infrastructure.Persistence;

/// <summary>
/// The single EF Core context for the shared PostgreSQL database (D-008, D-009). It stays inside
/// Infrastructure: Application and Server reach persistence only through Application-owned
/// interfaces (D-002). Identity tables join this context with #11.
/// </summary>
internal sealed class FamilyGraphDbContext(DbContextOptions<FamilyGraphDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FamilyGraphDbContext).Assembly);
        SnakeCaseNames.Apply(modelBuilder);
    }
}
