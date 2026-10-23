using FamilyGraph.Business.People;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyGraph.Infrastructure.Persistence.Configurations;

internal sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("people");
        builder.HasKey(person => person.Id);
        builder.Property(person => person.Id).HasConversion(ValueConverters.PersonId).ValueGeneratedNever();
        builder.Property(person => person.MaintainerId).HasConversion(ValueConverters.AccountId);
        builder.Property(person => person.Visibility).HasConversion(ValueConverters.Enum<ProfileVisibility>());
        builder.ComplexProperty(person => person.Name, name =>
        {
            name.Property(part => part.FirstName).HasColumnName("first_name");
            name.Property(part => part.MiddleName).HasColumnName("middle_name");
            name.Property(part => part.LastName).HasColumnName("last_name");
        });
        builder.Ignore(person => person.IsDeleted);
        builder.Ignore(person => person.PendingEvents);

        builder.HasIndex(person => person.MaintainerId).HasDatabaseName("ix_people_maintainer_id");
    }
}
