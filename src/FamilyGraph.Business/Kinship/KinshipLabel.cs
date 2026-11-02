namespace FamilyGraph.Business.Kinship;

/// <summary>
/// A relationship label written once per language of its culture (D-048), such as काका in Nepali
/// and "Kaka" in English. Display text only, never a stored fact (PRD 08).
/// </summary>
public sealed record KinshipLabel
{
    public KinshipLabel(IReadOnlyDictionary<string, string> texts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        if (texts.Count == 0 || texts.Values.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("A label needs a text in each language.", nameof(texts));
        }
        Texts = texts;
    }

    /// <summary>The text per language code.</summary>
    public IReadOnlyDictionary<string, string> Texts { get; }
}
