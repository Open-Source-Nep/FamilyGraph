using FamilyGraph.Application.People;
using FamilyGraph.Application.Persistence;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Business;
using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Accounts;

/// <summary>
/// The administrator's Users page and profile transfers (D-035): list the accounts and the profiles
/// each maintains, and make another account the maintainer of one profile or of all an account's
/// profiles, without that account accepting. Links and their consent never change.
/// </summary>
public sealed class AccountAdministrationService(
    IAccountStore accounts,
    IAccountQueries accountQueries,
    IPersonStore people,
    IPersonQueries personQueries,
    IAuditLog audit,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    AccountLimits accountLimits,
    PeopleLimits peopleLimits)
{
    public Task<AccountList> ListAccountsAsync(Viewer viewer, CancellationToken cancellationToken)
    {
        EnsureAdministrator(viewer);
        return accountQueries.ListAsync(accountLimits.ListLimit, cancellationToken);
    }

    /// <summary>The non-deleted profiles the account maintains, Private ones with their names (PRD 05).</summary>
    public async Task<ProfileList> ListProfilesAsync(Viewer viewer, AccountId accountId, CancellationToken cancellationToken)
    {
        EnsureAdministrator(viewer);
        _ = await accounts.FindAsync(accountId, cancellationToken) ?? throw new AccountNotFoundException();
        return await personQueries.ListMaintainedByAsync(accountId, peopleLimits.MaintainedListLimit, cancellationToken);
    }

    /// <summary>Makes <paramref name="toAccountId"/> the profile's maintainer.</summary>
    public async Task TransferProfileAsync(
        Viewer viewer, PersonId personId, AccountId toAccountId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);

        EnsureCanTransfer(viewer);
        var person = await people.FindAsync(personId, cancellationToken) ?? throw new PersonNotFoundException();
        _ = await accounts.FindAsync(toAccountId, cancellationToken) ?? throw new AccountNotFoundException();
        person.Transfer(toAccountId, viewer.AccountId, viewer.IsAdministrator);
        audit.Add(person.TakeEvents(), time.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Makes <paramref name="toAccountId"/> the maintainer of every non-deleted profile
    /// <paramref name="fromAccountId"/> maintains, in one transaction, and returns how many moved.
    /// Each profile records its own transfer.
    /// </summary>
    public async Task<int> TransferAllAsync(
        Viewer viewer, AccountId fromAccountId, AccountId toAccountId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(viewer);

        EnsureCanTransfer(viewer);
        _ = await accounts.FindAsync(fromAccountId, cancellationToken) ?? throw new AccountNotFoundException();
        _ = await accounts.FindAsync(toAccountId, cancellationToken) ?? throw new AccountNotFoundException();
        if (fromAccountId == toAccountId)
        {
            throw new TransferToSameAccountException();
        }

        var maintained = await people.ListMaintainedByAsync(fromAccountId, cancellationToken);
        foreach (var person in maintained)
        {
            person.Transfer(toAccountId, viewer.AccountId, viewer.IsAdministrator);
        }

        audit.Add([.. maintained.SelectMany(person => person.TakeEvents())], time.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return maintained.Count;
    }

    private static void EnsureAdministrator(Viewer viewer)
    {
        ArgumentNullException.ThrowIfNull(viewer);

        if (!viewer.IsAdministrator)
        {
            throw new AccountsNotAdministratorException();
        }
    }

    /// <summary>Refuses a non-administrator before anything is looked up, so nothing about other accounts leaks.</summary>
    private static void EnsureCanTransfer(Viewer viewer)
    {
        if (!viewer.IsAdministrator)
        {
            throw new TransferNotAdministratorException();
        }
    }
}

public sealed class TransferToSameAccountException()
    : ConflictException("transfer.same_account", "Profiles cannot be transferred to the account that maintains them.");
