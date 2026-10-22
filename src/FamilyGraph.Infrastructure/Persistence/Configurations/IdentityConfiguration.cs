using FamilyGraph.Application.Accounts;
using FamilyGraph.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FamilyGraph.Infrastructure.Persistence.Configurations;

/// <summary>
/// Names the ASP.NET Core Identity tables and indexes in lower snake_case (D-017) and seeds the
/// two roles (D-008). Applied after Identity's own model configuration.
/// </summary>
internal static class IdentityConfiguration
{
    // Fixed so the seed data never changes between migrations.
    internal static readonly IdentityRole<Guid> UserRole = Role("0199f3a0-0000-7000-8000-000000000001", AccountRoles.User);
    internal static readonly IdentityRole<Guid> AdministratorRole = Role("0199f3a0-0000-7000-8000-000000000002", AccountRoles.Administrator);

    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAccount>(builder =>
        {
            builder.ToTable("user_accounts");
            builder.HasIndex(user => user.NormalizedUserName).HasDatabaseName("ix_user_accounts_normalized_user_name");
            builder.HasIndex(user => user.NormalizedEmail).HasDatabaseName("ix_user_accounts_normalized_email");
        });
        modelBuilder.Entity<IdentityUserClaim<Guid>>(builder =>
        {
            builder.ToTable("user_account_claims");
            builder.HasIndex(claim => claim.UserId).HasDatabaseName("ix_user_account_claims_user_id");
        });
        modelBuilder.Entity<IdentityUserLogin<Guid>>(builder =>
        {
            builder.ToTable("user_account_logins");
            builder.HasIndex(login => login.UserId).HasDatabaseName("ix_user_account_logins_user_id");
        });
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("user_account_tokens");
        modelBuilder.Entity<IdentityUserRole<Guid>>(builder =>
        {
            builder.ToTable("user_account_roles");
            builder.HasIndex(assignment => assignment.RoleId).HasDatabaseName("ix_user_account_roles_role_id");
        });
        modelBuilder.Entity<IdentityRoleClaim<Guid>>(builder =>
        {
            builder.ToTable("role_claims");
            builder.HasIndex(claim => claim.RoleId).HasDatabaseName("ix_role_claims_role_id");
        });
        modelBuilder.Entity<IdentityRole<Guid>>(builder =>
        {
            builder.ToTable("roles");
            builder.HasIndex(role => role.NormalizedName).HasDatabaseName("ix_roles_normalized_name");
            builder.HasData(UserRole, AdministratorRole);
        });
    }

    private static IdentityRole<Guid> Role(string id, string name) => new()
    {
        Id = Guid.Parse(id),
        Name = name,
        NormalizedName = name.ToUpperInvariant(),
        ConcurrencyStamp = id,
    };
}
