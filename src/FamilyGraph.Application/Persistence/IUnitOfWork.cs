namespace FamilyGraph.Application.Persistence;

/// <summary>
/// Saves the changes made through the stores and the audit log of one request in a single
/// transaction (D-002), so a change is never saved without its audit events.
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
