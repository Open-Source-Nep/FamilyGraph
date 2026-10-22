using FamilyGraph.Application.Persistence;

namespace FamilyGraph.Infrastructure.Persistence;

/// <summary>One <c>SaveChanges</c> writes the tracked changes and the added audit rows in one transaction (D-002).</summary>
internal sealed class EfUnitOfWork(FamilyGraphDbContext context) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);
}
