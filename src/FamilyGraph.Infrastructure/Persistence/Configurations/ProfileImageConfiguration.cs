using FamilyGraph.Business.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyGraph.Infrastructure.Persistence.Configurations;

/// <summary>
/// Profile images as <c>bytea</c> in their own table, so loading a person never loads one (D-028).
/// <c>people.image_id</c> names the current image without a foreign key, which would make the two
/// tables depend on each other.
/// </summary>
internal sealed class ProfileImageConfiguration : IEntityTypeConfiguration<ProfileImage>
{
    public void Configure(EntityTypeBuilder<ProfileImage> builder)
    {
        builder.ToTable("profile_images", table =>
        {
            table.HasCheckConstraint("ck_profile_images_size", $"octet_length(content) <= {ProfileImage.MaxBytes}");
            table.HasCheckConstraint(
                "ck_profile_images_format", $"format IN ({string.Join(", ", ImageFormat.All.Select(f => f.Value))})");
        });
        builder.HasKey(image => image.Id);
        builder.Property(image => image.Id).HasConversion(ValueConverters.ProfileImageId).ValueGeneratedNever();
        builder.Property(image => image.PersonId).HasConversion(ValueConverters.PersonId);
        builder.Property(image => image.Format).HasConversion(ValueConverters.Enum<ImageFormat>());
        builder.Property(image => image.Content);

        builder.HasOne<Person>().WithMany().HasForeignKey(image => image.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(image => image.PersonId, "ix_profile_images_person_id");
    }
}
