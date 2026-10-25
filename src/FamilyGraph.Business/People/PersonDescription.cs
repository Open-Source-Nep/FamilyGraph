namespace FamilyGraph.Business.People;

/// <summary>
/// A person's description (PRD 01, D-037): one optional line of at most <see cref="MaxLength"/>
/// characters, such as "Teacher, lives in Pokhara", that tells people with the same name apart.
/// </summary>
public static class PersonDescription
{
    public const int MaxLength = 200;

    /// <summary>The trimmed text, or <see langword="null"/> when blank.</summary>
    public static string? Normalize(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var trimmed = description.Trim();
        if (trimmed.Contains('\n') || trimmed.Contains('\r'))
        {
            throw new DescriptionNotOneLineException();
        }

        return trimmed.Length > MaxLength ? throw new DescriptionTooLongException() : trimmed;
    }
}

public sealed class DescriptionTooLongException()
    : DomainException("person.description_too_long", $"A description has at most {PersonDescription.MaxLength} characters.");

public sealed class DescriptionNotOneLineException()
    : DomainException("person.description_not_one_line", "A description is one line.");
