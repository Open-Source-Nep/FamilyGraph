namespace FamilyGraph.Business.Audit;

/// <summary>An entity that records an <see cref="AuditEvent"/> for each change it makes (D-015).</summary>
public abstract class AuditedEntity
{
    private readonly List<AuditEvent> _pendingEvents = [];

    /// <summary>Events recorded since they were last taken, oldest first.</summary>
    public IReadOnlyList<AuditEvent> PendingEvents => _pendingEvents;

    /// <summary>Returns the pending events and clears them. Application calls this when saving.</summary>
    public IReadOnlyList<AuditEvent> TakeEvents()
    {
        var events = _pendingEvents.ToList();
        _pendingEvents.Clear();
        return events;
    }

    protected void Record(AuditEvent auditEvent) => _pendingEvents.Add(auditEvent);
}
