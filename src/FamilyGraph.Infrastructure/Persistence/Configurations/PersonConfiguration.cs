using FamilyGraph.Business.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyGraph.Infrastructure.Persistence.Configurations;

internal sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("people", table => table.HasCheckConstraint(
            "ck_people_description_length", $"char_length(description) <= {PersonDescription.MaxLength}"));
        builder.HasKey(person => person.Id);
        builder.Property(person => person.Id).HasConversion(ValueConverters.PersonId).ValueGeneratedNever();
        builder.Property(person => person.MaintainerId).HasConversion(ValueConverters.AccountId);
        builder.Property(person => person.ImageId).HasConversion(ValueConverters.ProfileImageId);
        builder.Property(person => person.Visibility).HasConversion(ValueConverters.Enum<ProfileVisibility>());
        builder.Property(person => person.Gender).HasConversion(ValueConverters.Enum<Gender>());
        builder.ComplexProperty(person => person.Name, name =>
        {
            name.Property(part => part.FirstName).HasColumnName("first_name");
            name.Property(part => part.MiddleName).HasColumnName("middle_name");
            name.Property(part => part.LastName).HasColumnName("last_name");
        });
        builder.ComplexProperty(person => person.BirthDate, date =>
        {
            date.Property(part => part.Calendar).HasColumnName("birth_calendar").HasConversion(ValueConverters.Enum<BirthCalendar>());
            date.Property(part => part.Year).HasColumnName("birth_year");
            date.Property(part => part.Month).HasColumnName("birth_month");
            date.Property(part => part.Day).HasColumnName("birth_day");
            date.Property(part => part.SortDate).HasColumnName("birth_sort_date");
            date.Ignore(part => part.IsFull);
        });
        builder.Ignore(person => person.IsDeleted);
        builder.Ignore(person => person.PendingEvents);

        builder.HasIndex(person => person.MaintainerId).HasDatabaseName("ix_people_maintainer_id");
    }
}
