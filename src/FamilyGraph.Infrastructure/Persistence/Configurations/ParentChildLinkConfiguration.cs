using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyGraph.Infrastructure.Persistence.Configurations;

/// <summary>
/// The relationship row stores both endpoint approvals and a nullable deletion timestamp, with no
/// status column; the indexes are the ones D-009 lists.
/// </summary>
internal sealed class ParentChildLinkConfiguration : IEntityTypeConfiguration<ParentChildLink>
{
    /// <summary>The row is active when both sides are approved and it is not deleted (D-009).</summary>
    internal static readonly string ActiveFilter =
        $"deleted_at IS NULL AND parent_approval = {EndpointApproval.Approved.Value} AND child_approval = {EndpointApproval.Approved.Value}";

    /// <summary>At most one non-deleted link per (parent, child) pair (PRD 10).</summary>
    internal const string UniqueParentChildIndex = "ux_parent_child_links_parent_child";

    private const string NotDeletedFilter = "deleted_at IS NULL";

    public void Configure(EntityTypeBuilder<ParentChildLink> builder)
    {
        builder.ToTable("parent_child_links", table =>
        {
            table.HasCheckConstraint("ck_parent_child_links_not_self", "parent_id <> child_id");
            table.HasCheckConstraint("ck_parent_child_links_type", ValueIn("type", ParentChildType.All.Select(t => t.Value)));
            table.HasCheckConstraint(
                "ck_parent_child_links_parent_approval", ValueIn("parent_approval", EndpointApproval.All.Select(a => a.Value)));
            table.HasCheckConstraint(
                "ck_parent_child_links_child_approval", ValueIn("child_approval", EndpointApproval.All.Select(a => a.Value)));
            table.HasCheckConstraint("ck_parent_child_links_birth_order", "birth_order IS NULL OR birth_order >= 1");
        });
        builder.HasKey(link => link.Id);
        builder.Property(link => link.Id).HasConversion(ValueConverters.LinkId).ValueGeneratedNever();
        builder.Property(link => link.ParentId).HasConversion(ValueConverters.PersonId);
        builder.Property(link => link.ChildId).HasConversion(ValueConverters.PersonId);
        builder.Property(link => link.Type).HasConversion(ValueConverters.Enum<ParentChildType>());
        builder.Property(link => link.ParentApproval).HasConversion(ValueConverters.Enum<EndpointApproval>());
        builder.Property(link => link.ChildApproval).HasConversion(ValueConverters.Enum<EndpointApproval>());
        builder.Property(link => link.DeletedAt);
        builder.Property(link => link.BirthOrder);
        builder.Ignore(link => link.PendingEvents);

        builder.HasOne<Person>().WithMany().HasForeignKey(link => link.ParentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(link => link.ChildId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(link => new { link.ParentId, link.ChildId }, UniqueParentChildIndex)
            .IsUnique()
            .HasFilter(NotDeletedFilter);
        builder.HasIndex(link => link.ParentId, "ix_parent_child_links_parent_active")
            .HasFilter(ActiveFilter);
        builder.HasIndex(link => link.ChildId, "ix_parent_child_links_child_active")
            .HasFilter(ActiveFilter);
        builder.HasIndex(link => link.ChildId, "ix_parent_child_links_child_not_deleted")
            .HasFilter(NotDeletedFilter);
    }

    private static string ValueIn(string column, IEnumerable<int> values) => $"{column} IN ({string.Join(", ", values)})";
}
