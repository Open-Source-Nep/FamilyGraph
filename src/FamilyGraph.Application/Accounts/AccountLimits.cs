namespace FamilyGraph.Application.Accounts;

/// <summary>Configurable list limits for accounts (D-010): <c>Accounts:ListLimit</c> caps the administrator's Users page (D-035).</summary>
public sealed record AccountLimits(int ListLimit = AccountLimits.DefaultListLimit)
{
    public const int DefaultListLimit = 1000;
}
