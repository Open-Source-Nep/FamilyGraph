namespace FamilyGraph.Application.Accounts;

/// <summary>
/// The configured list of administrator email addresses (D-008, <c>Admin:Emails</c>). Matching
/// ignores case and surrounding spaces. An unverified email never matches.
/// </summary>
public sealed class AdministratorEmails
{
    private readonly HashSet<string> _emails;

    public AdministratorEmails(IEnumerable<string> emails)
    {
        _emails = new HashSet<string>(
            emails.Select(email => email.Trim()).Where(email => email.Length > 0),
            StringComparer.OrdinalIgnoreCase);
    }

    public bool Matches(string email, bool emailVerified) => emailVerified && _emails.Contains(email.Trim());
}
