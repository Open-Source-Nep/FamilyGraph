using FamilyGraph.Business.Claims;
using FamilyGraph.Business.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyGraph.Infrastructure.Persistence.Configurations;

/// <summary>
/// Identity claims (D-051). <see cref="ProfileClaim.State"/> is a concurrency token, so a claim is
/// decided or withdrawn only once when two requests race.
/// </summary>
internal sealed class ProfileClaimConfiguration : IEntityTypeConfiguration<ProfileClaim>
{
    /// <summary>At most one open claim per account (D-051).</summary>
    internal const string UniqueOpenPerClaimantIndex = "ux_profile_claims_claimant_open";

    public void Configure(EntityTypeBuilder<ProfileClaim> builder)
    {
        builder.ToTable("profile_claims", table =>
        {
            table.HasCheckConstraint("ck_profile_claims_state", $"state IN ({string.Join(", ", ClaimState.All.Select(s => s.Value))})");
            table.HasCheckConstraint("ck_profile_claims_evidence_length", $"char_length(evidence) <= {ClaimEvidence.MaxLength}");
            table.HasCheckConstraint(
                "ck_profile_claims_texts_length",
                $"char_length(rationale) <= {ClaimText.MaxLength} AND char_length(administrator_question) <= {ClaimText.MaxLength}"
                + $" AND char_length(maintainer_response) <= {ClaimText.MaxLength}");
        });
        builder.HasKey(claim => claim.Id);
        builder.Property(claim => claim.Id).HasConversion(ValueConverters.ClaimId).ValueGeneratedNever();
        builder.Property(claim => claim.PersonId).HasConversion(ValueConverters.PersonId);
        builder.Property(claim => claim.ClaimantId).HasConversion(ValueConverters.AccountId);
        builder.Property(claim => claim.Evidence);
        builder.Property(claim => claim.SubmittedAt);
        builder.Property(claim => claim.State).HasConversion(ValueConverters.Enum<ClaimState>()).IsConcurrencyToken();
        builder.Property(claim => claim.ClosedById).HasConversion(ValueConverters.AccountId);
        builder.Property(claim => claim.ReviewRequestedAt);
        builder.Property(claim => claim.Appealed);
        builder.Property(claim => claim.DecidedByAdministrator);
        builder.Property(claim => claim.Rationale);
        builder.Property(claim => claim.AdministratorQuestion);
        builder.Property(claim => claim.MaintainerResponse);
        builder.Property(claim => claim.NotesErasedAt);
        builder.Ignore(claim => claim.IsOpen);
        builder.Ignore(claim => claim.ReviewRequested);
        builder.Ignore(claim => claim.PendingEvents);

        builder.HasOne<Person>().WithMany().HasForeignKey(claim => claim.PersonId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(claim => claim.PersonId).HasDatabaseName("ix_profile_claims_person_id");
        builder.HasIndex(claim => claim.ClosedAt)
            .HasDatabaseName("ix_profile_claims_notes_to_erase")
            .HasFilter("notes_erased_at IS NULL AND closed_at IS NOT NULL");
        builder.HasIndex(claim => claim.ClaimantId, UniqueOpenPerClaimantIndex)
            .IsUnique()
            .HasFilter($"state = {ClaimState.Open.Value}");
    }
}
