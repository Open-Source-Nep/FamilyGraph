using FamilyGraph.Business.Handovers;
using FamilyGraph.Business.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyGraph.Infrastructure.Persistence.Configurations;

/// <summary>
/// Handover links, looked up by the hash of their secret. <see cref="ProfileHandover.State"/> is a
/// concurrency token, so two accounts accepting the same link at once cannot both succeed.
/// </summary>
internal sealed class ProfileHandoverConfiguration : IEntityTypeConfiguration<ProfileHandover>
{
    /// <summary>At most one open link per profile.</summary>
    internal const string UniqueOpenPerPersonIndex = "ux_profile_handovers_person_open";

    public void Configure(EntityTypeBuilder<ProfileHandover> builder)
    {
        builder.ToTable("profile_handovers", table => table.HasCheckConstraint(
            "ck_profile_handovers_state", $"state IN ({string.Join(", ", HandoverState.All.Select(s => s.Value))})"));
        builder.HasKey(handover => handover.Id);
        builder.Property(handover => handover.Id).HasConversion(ValueConverters.HandoverId).ValueGeneratedNever();
        builder.Property(handover => handover.PersonId).HasConversion(ValueConverters.PersonId);
        builder.Property(handover => handover.OfferedById).HasConversion(ValueConverters.AccountId);
        builder.Property(handover => handover.TokenHash).HasMaxLength(64);
        builder.Property(handover => handover.CreatedAt);
        builder.Property(handover => handover.ExpiresAt);
        builder.Property(handover => handover.State).HasConversion(ValueConverters.Enum<HandoverState>()).IsConcurrencyToken();
        builder.Property(handover => handover.AcceptedById).HasConversion(ValueConverters.AccountId);
        builder.Ignore(handover => handover.PendingEvents);

        builder.HasOne<Person>().WithMany().HasForeignKey(handover => handover.PersonId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(handover => handover.TokenHash, "ux_profile_handovers_token_hash").IsUnique();
        builder.HasIndex(handover => handover.PersonId, UniqueOpenPerPersonIndex)
            .IsUnique()
            .HasFilter($"state = {HandoverState.Open.Value}");
    }
}
