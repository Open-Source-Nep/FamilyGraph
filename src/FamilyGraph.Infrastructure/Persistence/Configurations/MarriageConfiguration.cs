using FamilyGraph.Business.People;
using FamilyGraph.Business.Relationships;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyGraph.Infrastructure.Persistence.Configurations;

/// <summary>
/// A marriage row stores both spouses in ID order, both approvals, the former flag and a nullable
/// deletion timestamp (D-031). The ID order lets one unique index keep a pair to one marriage, and
/// the active-marriage indexes on each spouse serve the graph walks.
/// </summary>
internal sealed class MarriageConfiguration : IEntityTypeConfiguration<Marriage>
{
    /// <summary>The row is active when both sides are approved and it is not deleted.</summary>
    internal static readonly string ActiveFilter =
        $"deleted_at IS NULL AND first_approval = {EndpointApproval.Approved.Value} AND second_approval = {EndpointApproval.Approved.Value}";

    /// <summary>At most one non-deleted marriage per pair (D-031).</summary>
    internal const string UniquePairIndex = "ux_marriages_spouses";

    private const string NotDeletedFilter = "deleted_at IS NULL";

    public void Configure(EntityTypeBuilder<Marriage> builder)
    {
        builder.ToTable("marriages", table =>
        {
            table.HasCheckConstraint("ck_marriages_spouse_order", "first_spouse_id < second_spouse_id");
            table.HasCheckConstraint(
                "ck_marriages_first_approval", ValueIn("first_approval", EndpointApproval.All.Select(a => a.Value)));
            table.HasCheckConstraint(
                "ck_marriages_second_approval", ValueIn("second_approval", EndpointApproval.All.Select(a => a.Value)));
        });
        builder.HasKey(marriage => marriage.Id);
        builder.Property(marriage => marriage.Id).HasConversion(ValueConverters.LinkId).ValueGeneratedNever();
        builder.Property(marriage => marriage.FirstSpouseId).HasConversion(ValueConverters.PersonId);
        builder.Property(marriage => marriage.SecondSpouseId).HasConversion(ValueConverters.PersonId);
        builder.Property(marriage => marriage.FirstApproval).HasConversion(ValueConverters.Enum<EndpointApproval>());
        builder.Property(marriage => marriage.SecondApproval).HasConversion(ValueConverters.Enum<EndpointApproval>());
        builder.Property(marriage => marriage.IsFormer);
        builder.Property(marriage => marriage.DeletedAt);
        builder.Ignore(marriage => marriage.PendingEvents);

        builder.HasOne<Person>().WithMany().HasForeignKey(marriage => marriage.FirstSpouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(marriage => marriage.SecondSpouseId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(marriage => new { marriage.FirstSpouseId, marriage.SecondSpouseId }, UniquePairIndex)
            .IsUnique()
            .HasFilter(NotDeletedFilter);
        builder.HasIndex(marriage => marriage.FirstSpouseId, "ix_marriages_first_spouse_active")
            .HasFilter(ActiveFilter);
        builder.HasIndex(marriage => marriage.SecondSpouseId, "ix_marriages_second_spouse_active")
            .HasFilter(ActiveFilter);
    }

    private static string ValueIn(string column, IEnumerable<int> values) => $"{column} IN ({string.Join(", ", values)})";
}
