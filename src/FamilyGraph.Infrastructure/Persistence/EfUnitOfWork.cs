using FamilyGraph.Application.Persistence;
using FamilyGraph.Business.Handovers;
using FamilyGraph.Business.Relationships;
using FamilyGraph.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FamilyGraph.Infrastructure.Persistence;

/// <summary>One <c>SaveChanges</c> writes the tracked changes and the added audit rows in one transaction (D-002).</summary>
internal sealed class EfUnitOfWork(FamilyGraphDbContext context) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: ParentChildLinkConfiguration.UniqueParentChildIndex,
        })
        {
            // Two requests linked the same pair at once; the domain check could not see the other one.
            throw new DuplicateLinkException();
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: MarriageConfiguration.UniquePairIndex,
        })
        {
            throw new DuplicateMarriageException();
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: ProfileHandoverConfiguration.UniqueOpenPerPersonIndex,
        })
        {
            throw new HandoverAlreadyOpenException();
        }
        catch (DbUpdateConcurrencyException e) when (e.Entries.Any(entry => entry.Entity is ProfileHandover))
        {
            // Someone else accepted or cancelled the handover link first.
            throw new HandoverClosedException();
        }
    }
}
