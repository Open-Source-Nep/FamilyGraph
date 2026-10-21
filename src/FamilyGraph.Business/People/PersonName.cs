namespace FamilyGraph.Business.People;

/// <summary>
/// A person's name (PRD 01): a required first name and optional middle and last names.
/// Parts are trimmed; blank optional parts are treated as absent.
/// </summary>
public sealed record PersonName
{
    public PersonName(string firstName, string? middleName = null, string? lastName = null)
    {
        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new FirstNameRequiredException();
        }

        FirstName = Normalize(firstName)!;
        MiddleName = Normalize(middleName);
        LastName = Normalize(lastName);
    }

    public string FirstName { get; }

    public string? MiddleName { get; }

    public string? LastName { get; }

    private static string? Normalize(string? part) => string.IsNullOrWhiteSpace(part) ? null : part.Trim();
}

public sealed class FirstNameRequiredException()
    : DomainException("person.first_name_required", "A person must have a first name.");
