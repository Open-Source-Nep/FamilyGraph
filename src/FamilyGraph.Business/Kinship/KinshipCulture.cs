namespace FamilyGraph.Business.Kinship;

/// <summary>
/// A culture, which exists only to define relationship labels (D-048): its languages, first the
/// one it falls back to, its labels in each of them, its name in each of them and possibly in
/// other languages (for the culture switch), its rules, and the
/// <see cref="Fallback"/> label for a path no rule matches.
/// </summary>
public sealed record KinshipCulture
{
    public KinshipCulture(
        string code,
        IReadOnlyList<string> languages,
        IReadOnlyDictionary<string, string> names,
        KinshipLabel fallback,
        IReadOnlyList<KinshipRule> rules)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(languages);
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(fallback);
        ArgumentNullException.ThrowIfNull(rules);
        if (languages.Count == 0 || languages.Distinct(StringComparer.Ordinal).Count() != languages.Count)
        {
            throw new ArgumentException("A culture needs one or more distinct languages.", nameof(languages));
        }
        if (languages.Any(language => !names.TryGetValue(language, out var name) || string.IsNullOrWhiteSpace(name)))
        {
            throw new ArgumentException($"The culture's name needs a text in each of its languages ({string.Join(", ", languages)}).", nameof(names));
        }
        CheckLanguages(fallback.Texts, languages, "fallback label");
        foreach (var rule in rules)
        {
            CheckLanguages(rule.Label.Texts, languages, "label");
        }

        Code = code;
        Languages = languages;
        Names = names;
        Fallback = fallback;
        Rules = rules;
    }

    public string Code { get; }

    public IReadOnlyList<string> Languages { get; }

    public IReadOnlyDictionary<string, string> Names { get; }

    public KinshipLabel Fallback { get; }

    public IReadOnlyList<KinshipRule> Rules { get; }

    /// <summary>
    /// The label for the person a path reaches, as seen from its start: the most specific matching
    /// rule (the first of equally specific ones), else <see cref="Fallback"/>. A path without steps
    /// has no label. Facts that are hidden or unknown match only rules that do not ask for them, so
    /// a label never reveals them (PRD 05).
    /// </summary>
    public KinshipLabel? Label(KinshipFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.Steps.Count == 0)
        {
            return null;
        }

        KinshipRule? best = null;
        foreach (var rule in Rules)
        {
            if (rule.Matches(facts) && (best is null || rule.Specificity > best.Specificity))
            {
                best = rule;
            }
        }
        return best?.Label ?? Fallback;
    }

    /// <summary>A label's text in <paramref name="language"/>, or in the culture's first language.</summary>
    public string Text(KinshipLabel label, string? language)
    {
        ArgumentNullException.ThrowIfNull(label);
        return language is not null && label.Texts.TryGetValue(language, out var text) ? text : label.Texts[Languages[0]];
    }

    private static void CheckLanguages(IReadOnlyDictionary<string, string> texts, IReadOnlyList<string> languages, string what)
    {
        if (texts.Count != languages.Count || languages.Any(language => !texts.ContainsKey(language)))
        {
            throw new ArgumentException($"Every {what} needs a text in exactly the culture's languages ({string.Join(", ", languages)}).");
        }
    }
}
