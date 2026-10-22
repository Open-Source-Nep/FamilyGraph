using FamilyGraph.Application.Accounts;

namespace FamilyGraph.Application.Tests.Accounts;

public class AccountServiceTests
{
    private readonly InMemoryAccountStore _store = new();

    private static ExternalLogin Google(string email = "maya@example.org", bool verified = true, string key = "google-1") =>
        new("Google", key, email, verified);

    private AccountService Service(params string[] administrators) => new(_store, new AdministratorEmails(administrators));

    [Fact]
    [Trait("AC", "AC-01.12")]
    public async Task First_sign_in_registers_an_account_with_the_user_role()
    {
        var account = await Service().SignInAsync(Google(), TestContext.Current.CancellationToken);

        Assert.Equal("maya@example.org", account.Email);
        Assert.False(account.IsAdministrator);
        Assert.Equal([AccountRoles.User], account.Roles);
        Assert.Single(_store.Accounts);
    }

    [Fact]
    public async Task Later_sign_ins_return_the_same_account_and_refresh_its_email()
    {
        var service = Service();
        var first = await service.SignInAsync(Google(), TestContext.Current.CancellationToken);

        var second = await service.SignInAsync(Google(email: "maya@new.example.org"), TestContext.Current.CancellationToken);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("maya@new.example.org", second.Email);
        Assert.Single(_store.Accounts);
    }

    [Fact]
    public async Task Different_provider_keys_are_different_accounts()
    {
        var service = Service();

        var first = await service.SignInAsync(Google(key: "google-1"), TestContext.Current.CancellationToken);
        var second = await service.SignInAsync(Google(key: "google-2"), TestContext.Current.CancellationToken);

        Assert.NotEqual(first.Id, second.Id);
    }

    [Theory]
    [InlineData("maya@example.org")]
    [InlineData(" MAYA@Example.org ")]
    public async Task Verified_email_on_the_administrator_list_grants_the_administrator_role(string configured)
    {
        var account = await Service(configured).SignInAsync(Google(), TestContext.Current.CancellationToken);

        Assert.True(account.IsAdministrator);
        Assert.Equal([AccountRoles.User, AccountRoles.Administrator], account.Roles);
    }

    [Fact]
    public async Task Unverified_email_never_matches_the_administrator_list()
    {
        var account = await Service("maya@example.org").SignInAsync(Google(verified: false), TestContext.Current.CancellationToken);

        Assert.False(account.IsAdministrator);
    }

    [Fact]
    public async Task Administrator_removed_from_the_list_loses_the_role_on_next_sign_in()
    {
        var admin = await Service("maya@example.org").SignInAsync(Google(), TestContext.Current.CancellationToken);

        var later = await Service().SignInAsync(Google(), TestContext.Current.CancellationToken);

        Assert.True(admin.IsAdministrator);
        Assert.False(later.IsAdministrator);
        Assert.False(_store.Accounts[admin.Id].IsAdministrator);
    }

    [Fact]
    public async Task Get_returns_null_for_an_unknown_account()
    {
        var account = await Service().GetAsync(new(Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.Null(account);
    }
}
