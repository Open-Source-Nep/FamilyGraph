using FamilyGraph.Business;

namespace FamilyGraph.Application.Accounts;

/// <summary>Only administrators may list accounts and the profiles they maintain (D-035).</summary>
public sealed class AccountsNotAdministratorException()
    : NotPermittedException("accounts.not_administrator", "Only an administrator can list accounts.");
