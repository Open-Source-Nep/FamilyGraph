using FamilyGraph.Infrastructure.Identity;
using FamilyGraph.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FamilyGraph.Infrastructure.Persistence;

/// <summary>
/// The single EF Core context for the shared PostgreSQL database (D-008, D-009). It stays inside
/// Infrastructure: Application and Server reach persistence only through Application-owned
/// interfaces (D-002). It derives from <see cref="IdentityDbContext{TUser, TRole, TKey}"/> so
/// account changes and domain writes can share one transaction (D-008).
/// </summary>
internal sealed class FamilyGraphDbContext(DbContextOptions<FamilyGraphDbContext> options)
    : IdentityDbContext<UserAccount, IdentityRole<Guid>, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        IdentityConfiguration.Apply(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FamilyGraphDbContext).Assembly);
        SnakeCaseNames.Apply(modelBuilder);
    }
}
