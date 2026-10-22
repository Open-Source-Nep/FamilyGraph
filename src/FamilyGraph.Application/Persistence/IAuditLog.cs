using FamilyGraph.Business.Audit;

namespace FamilyGraph.Application.Persistence;

/// <summary>The append-only audit history (D-015). Added events are saved by <see cref="IUnitOfWork"/>.</summary>
public interface IAuditLog
{
    void Add(IEnumerable<AuditEvent> auditEvents, DateTimeOffset occurredAt);
}
