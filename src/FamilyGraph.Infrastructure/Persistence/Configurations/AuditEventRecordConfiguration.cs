using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FamilyGraph.Infrastructure.Persistence.Configurations;

internal sealed class AuditEventRecordConfiguration : IEntityTypeConfiguration<AuditEventRecord>
{
    public void Configure(EntityTypeBuilder<AuditEventRecord> builder)
    {
        builder.ToTable("audit_events");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).ValueGeneratedNever();
        builder.Property(record => record.OccurredAt);
        builder.Property(record => record.Family);
        builder.Property(record => record.Type).HasMaxLength(100);
        builder.Property(record => record.ActorId);
        builder.Property(record => record.SubjectId);
        builder.Property(record => record.Data).HasColumnType("jsonb");

        builder.HasIndex(record => new { record.SubjectId, record.OccurredAt }).HasDatabaseName("ix_audit_events_subject");
        builder.HasIndex(record => new { record.ActorId, record.OccurredAt }).HasDatabaseName("ix_audit_events_actor");
    }
}
