using FamilyGraph.Application.Persistence;
using FamilyGraph.Business.Audit;

namespace FamilyGraph.Infrastructure.Persistence;

internal sealed class EfAuditLog(FamilyGraphDbContext context) : IAuditLog
{
    public void Add(IEnumerable<AuditEvent> auditEvents, DateTimeOffset occurredAt) =>
        context.AddRange(auditEvents.Select(auditEvent => AuditEventRecord.From(auditEvent, occurredAt)));
}
