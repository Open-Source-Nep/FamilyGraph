using FamilyGraph.Business.People;

namespace FamilyGraph.Business.Audit;

/// <summary>
/// Something that happened to a domain entity, recorded by the entity itself (D-015). Events hold
/// IDs, types and states only, never names or other personal values. Application stamps the time
/// and saves them in the same transaction as the change (D-002).
/// </summary>
public abstract record AuditEvent(AccountId ActorId)
{
    public abstract AuditFamily Family { get; }
}
