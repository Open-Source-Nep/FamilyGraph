using FamilyGraph.Application.Accounts;
using FamilyGraph.Application.People;
using FamilyGraph.Application.Privacy;
using FamilyGraph.Application.Tests.People;
using FamilyGraph.Business.People;

namespace FamilyGraph.Application.Tests.Accounts;

/// <summary>The administrator's Users page and profile transfers (D-035).</summary>
public class AccountAdministrationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);

    private readonly InMemoryAccountStore _accounts = new();
    private readonly InMemoryPeople _people = new();
    private readonly AccountView _admin;
    private readonly AccountView _alice;
    private readonly AccountView _bob;

    public AccountAdministrationServiceTests()
    {
        _admin = AddAccount("admin@example.org", isAdministrator: true);
        _alice = AddAccount("alice@example.org");
        _bob = AddAccount("bob@example.org");
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private Viewer Admin => new(_admin.Id, IsAdministrator: true);

    private AccountAdministrationService Service(int accountListLimit = AccountLimits.DefaultListLimit) => new(
        _accounts,
        new FakeAccountQueries(_accounts, _people),
        _people,
        _people,
        _people,
        _people,
        new FixedTimeProvider(Now),
        new AccountLimits(accountListLimit),
        new PeopleLimits());

    [Fact]
    [Trait("AC", "AC-01.23")]
    public async Task Administrator_lists_accounts_with_their_flags_and_profile_counts()
    {
        await AddPersonAsync("Maya", _alice.Id);
        await AddPersonAsync("Hari", _alice.Id);

        var list = await Service().ListAccountsAsync(Admin, Cancellation);

        Assert.False(list.IsTruncated);
        Assert.Equal(
            [
                new AccountSummary(_admin.Id, "admin@example.org", true, true, 0),
                new AccountSummary(_alice.Id, "alice@example.org", true, false, 2),
                new AccountSummary(_bob.Id, "bob@example.org", true, false, 0),
            ],
            list.Accounts);
    }

    [Fact]
    public async Task The_account_list_stops_at_its_limit()
    {
        var list = await Service(accountListLimit: 2).ListAccountsAsync(Admin, Cancellation);

        Assert.Equal(2, list.Accounts.Count);
        Assert.True(list.IsTruncated);
    }

    [Fact]
    [Trait("AC", "AC-01.23")]
    public async Task Administrator_lists_the_profiles_an_account_maintains_including_private_ones()
    {
        var maya = await AddPersonAsync("Maya", _alice.Id);
        maya.SetVisibility(ProfileVisibility.Private, _alice.Id);
        await AddPersonAsync("Hari", _bob.Id);

        var list = await Service().ListProfilesAsync(Admin, _alice.Id, Cancellation);

        var only = Assert.Single(list.People);
        Assert.Equal((maya.Id, "Maya"), (only.Id, only.Name.FirstName));
    }

    [Fact]
    [Trait("AC", "AC-01.27")]
    public async Task A_user_who_is_not_an_administrator_cannot_list_accounts_or_profiles()
    {
        var alice = new Viewer(_alice.Id, IsAdministrator: false);

        await Assert.ThrowsAsync<AccountsNotAdministratorException>(() => Service().ListAccountsAsync(alice, Cancellation));
        await Assert.ThrowsAsync<AccountsNotAdministratorException>(() => Service().ListProfilesAsync(alice, _bob.Id, Cancellation));
    }

    [Fact]
    public async Task Listing_the_profiles_of_an_unknown_account_is_not_found()
    {
        await Assert.ThrowsAsync<AccountNotFoundException>(
            () => Service().ListProfilesAsync(Admin, new AccountId(Guid.NewGuid()), Cancellation));
    }

    [Fact]
    [Trait("AC", "AC-01.24")]
    [Trait("AC", "AC-01.26")]
    public async Task Administrator_transfers_one_profile_without_the_receivers_approval()
    {
        var maya = await AddPersonAsync("Maya", _alice.Id);

        await Service().TransferProfileAsync(Admin, maya.Id, _bob.Id, Cancellation);

        Assert.True(_people.People[maya.Id].IsMaintainedBy(_bob.Id));
        var (auditEvent, occurredAt) = Assert.Single(_people.AuditEvents, entry => entry.Event is PersonTransferred);
        Assert.Equal((new PersonTransferred(maya.Id, _admin.Id, _alice.Id, _bob.Id), Now), (auditEvent, occurredAt));
    }

    [Fact]
    [Trait("AC", "AC-01.27")]
    public async Task A_maintainer_who_is_not_an_administrator_cannot_transfer()
    {
        var maya = await AddPersonAsync("Maya", _alice.Id);
        var alice = new Viewer(_alice.Id, IsAdministrator: false);
        var saves = _people.SaveCount;

        await Assert.ThrowsAsync<TransferNotAdministratorException>(
            () => Service().TransferProfileAsync(alice, maya.Id, _bob.Id, Cancellation));
        await Assert.ThrowsAsync<TransferNotAdministratorException>(
            () => Service().TransferAllAsync(alice, _alice.Id, _bob.Id, Cancellation));

        Assert.True(_people.People[maya.Id].IsMaintainedBy(_alice.Id));
        Assert.Equal(saves, _people.SaveCount);
    }

    [Fact]
    public async Task Transfer_to_an_unknown_account_is_not_found()
    {
        var maya = await AddPersonAsync("Maya", _alice.Id);

        await Assert.ThrowsAsync<AccountNotFoundException>(
            () => Service().TransferProfileAsync(Admin, maya.Id, new AccountId(Guid.NewGuid()), Cancellation));
    }

    [Fact]
    [Trait("AC", "AC-01.25")]
    [Trait("AC", "AC-01.26")]
    public async Task Administrator_transfers_all_of_an_accounts_profiles_and_each_is_recorded()
    {
        var maya = await AddPersonAsync("Maya", _alice.Id);
        var hari = await AddPersonAsync("Hari", _alice.Id);
        var deleted = await AddPersonAsync("Gone", _alice.Id);
        deleted.Delete(_alice.Id, actorIsAdministrator: false, Now);
        var other = await AddPersonAsync("Sita", _bob.Id);
        var saves = _people.SaveCount;

        var count = await Service().TransferAllAsync(Admin, _alice.Id, _bob.Id, Cancellation);

        Assert.Equal(2, count);
        Assert.Equal(saves + 1, _people.SaveCount);
        Assert.All([maya, hari, other], person => Assert.True(_people.People[person.Id].IsMaintainedBy(_bob.Id)));
        Assert.True(_people.People[deleted.Id].IsMaintainedBy(_alice.Id));
        Assert.Equal(
            new HashSet<PersonId> { maya.Id, hari.Id },
            _people.AuditEvents.Select(entry => entry.Event).OfType<PersonTransferred>().Select(e => e.PersonId).ToHashSet());
    }

    [Fact]
    public async Task Transferring_all_profiles_to_the_same_account_is_refused()
    {
        var error = await Assert.ThrowsAsync<TransferToSameAccountException>(
            () => Service().TransferAllAsync(Admin, _alice.Id, _alice.Id, Cancellation));

        Assert.Equal("transfer.same_account", error.Code);
    }

    private AccountView AddAccount(string email, bool isAdministrator = false)
    {
        var account = new AccountView(new AccountId(Guid.NewGuid()), email, EmailVerified: true, isAdministrator);
        _accounts.Accounts[account.Id] = account;
        return account;
    }

    private async Task<Person> AddPersonAsync(string firstName, AccountId maintainer)
    {
        var person = Person.Create(new PersonName(firstName), maintainer);
        person.TakeEvents();
        _people.Add(person);
        await _people.SaveChangesAsync(Cancellation);
        return person;
    }

    private sealed class FakeAccountQueries(InMemoryAccountStore accounts, InMemoryPeople people) : IAccountQueries
    {
        public Task<AccountList> ListAsync(int limit, CancellationToken cancellationToken)
        {
            var all = accounts.Accounts.Values
                .OrderBy(account => account.Email, StringComparer.Ordinal)
                .Select(account => new AccountSummary(
                    account.Id,
                    account.Email,
                    account.EmailVerified,
                    account.IsAdministrator,
                    people.People.Values.Count(person => person.IsMaintainedBy(account.Id) && !person.IsDeleted)))
                .ToList();
            return Task.FromResult(new AccountList(all.Take(limit).ToList(), all.Count > limit));
        }
    }
}
