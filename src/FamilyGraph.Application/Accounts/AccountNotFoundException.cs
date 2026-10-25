namespace FamilyGraph.Application.Accounts;

public sealed class AccountNotFoundException()
    : NotFoundException("account.not_found", "This account does not exist.");
