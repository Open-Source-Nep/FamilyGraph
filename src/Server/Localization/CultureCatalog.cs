using System.Text.Json;
using FamilyGraph.Business.Graph;
using FamilyGraph.Business.Kinship;
using FamilyGraph.Business.People;

namespace Server.Localization;

/// <summary>
/// The cultures this instance offers for relationship labels (D-048), each a JSON file in
/// <c>Cultures</c> read once at startup. A culture exists only to define labels and lists the
/// languages it is written in; every one must be a language the instance offers (D-047). A file:
/// <code>
/// { "code": "ne", "languages": ["ne", "en"],
///   "name": { "ne": "नेपाली", "en": "Nepali" },
///   "fallback": { "ne": "नातेदार", "en": "Relative" },
///   "rules": [ { "path": "parent:male parent child:male:younger", "label": { "ne": "काका", "en": "Kaka" } } ] }
/// </code>
/// A rule's <c>path</c> is its steps from the start person, separated by spaces: <c>parent</c>,
/// <c>child</c> or <c>spouse</c>, each optionally followed by <c>:male</c>, <c>:female</c> or
/// <c>:other</c> (the gender of the person reached); a child step right after a parent step by
/// <c>:elder</c> or <c>:younger</c>; and any child step by <c>:nth=N</c> or <c>:nth=youngest</c>
/// (place among all the parent's children) or <c>:nth-gender=N</c> or <c>:nth-gender=youngest</c>
/// (among those of the child's gender). An optional <c>start</c> gives the start person's gender.
/// </summary>
public sealed class CultureCatalog
{
    private static readonly JsonSerializerOptions FileOptions = new(JsonSerializerDefaults.Web);

    private readonly Dictionary<string, KinshipCulture> cultures;

    private CultureCatalog(string defaultCulture, IReadOnlyList<KinshipCulture> available)
    {
        DefaultCulture = defaultCulture;
        Available = available;
        cultures = available.ToDictionary(culture => culture.Code, StringComparer.Ordinal);
    }

    /// <summary>The culture a browser without a remembered choice starts in.</summary>
    public string DefaultCulture { get; }

    /// <summary>Every culture, by code.</summary>
    public IReadOnlyList<KinshipCulture> Available { get; }

    /// <summary>The culture with <paramref name="code"/>, or the default one.</summary>
    public KinshipCulture FindOrDefault(string? code) =>
        code is not null && cultures.TryGetValue(code, out var culture) ? culture : cultures[DefaultCulture];

    /// <summary>
    /// Reads every <c>*.json</c> file in <paramref name="directory"/>. A file that cannot be read,
    /// whose <c>code</c> is not its file name, which names a language the instance does not offer,
    /// or a default culture without a file stops the startup.
    /// </summary>
    public static CultureCatalog Load(string directory, string defaultCulture, IReadOnlyCollection<string> languages)
    {
        var loaded = new List<KinshipCulture>();
        var files = Directory.Exists(directory) ? Directory.GetFiles(directory, "*.json") : [];
        foreach (var path in files.Order(StringComparer.Ordinal))
        {
            var culture = Read(path);
            if (culture.Code != Path.GetFileNameWithoutExtension(path))
            {
                throw new InvalidOperationException($"The culture file {path} must have its file name as \"code\".");
            }
            if (culture.Languages.FirstOrDefault(language => !languages.Contains(language)) is { } missing)
            {
                throw new InvalidOperationException($"The culture file {path} uses the language \"{missing}\", which has no translation file.");
            }
            loaded.Add(culture);
        }

        if (!loaded.Any(culture => culture.Code == defaultCulture))
        {
            throw new InvalidOperationException($"{CultureSettings.Section}:Default is \"{defaultCulture}\", but there is no culture file for it.");
        }
        return new CultureCatalog(defaultCulture, loaded);
    }

    private static KinshipCulture Read(string path)
    {
        CultureFile? file;
        try
        {
            using var stream = File.OpenRead(path);
            file = JsonSerializer.Deserialize<CultureFile>(stream, FileOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"The culture file {path} is not valid JSON.", exception);
        }

        if (file is not { Code.Length: > 0, Languages: { } languages, Name: { } name, Fallback: { } fallback, Rules: { } rules })
        {
            throw new InvalidOperationException($"The culture file {path} needs \"code\", \"languages\", \"name\", \"fallback\" and \"rules\".");
        }

        try
        {
            return new KinshipCulture(
                file.Code,
                languages,
                name,
                new KinshipLabel(fallback),
                [.. rules.Select((rule, index) => ReadRule(rule, index))]);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException($"The culture file {path} is not valid: {exception.Message}", exception);
        }
    }

    private static KinshipRule ReadRule(RuleFile rule, int index)
    {
        if (rule is not { Path.Length: > 0, Label: { } label })
        {
            throw new ArgumentException($"Rule {index + 1} needs \"path\" and \"label\".");
        }
        try
        {
            return new KinshipRule(
                [.. rule.Path.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(ReadStep)],
                new KinshipLabel(label),
                rule.Start is null ? null : ReadGender(rule.Start));
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException($"Rule {index + 1} (\"{rule.Path}\"): {exception.Message}", exception);
        }
    }

    private static KinshipStep ReadStep(string text)
    {
        var parts = text.Split(':');
        var relation = parts[0] switch
        {
            "parent" => StepRelation.Parent,
            "child" => StepRelation.Child,
            "spouse" => StepRelation.Spouse,
            _ => throw new ArgumentException($"\"{parts[0]}\" is not parent, child or spouse."),
        };

        Gender? gender = null;
        RelativeAge? age = null;
        ChildPosition? position = null;
        foreach (var part in parts.Skip(1))
        {
            switch (part)
            {
                case "male" or "female" or "other" when gender is null:
                    gender = ReadGender(part);
                    break;
                case "elder" when age is null:
                    age = RelativeAge.Elder;
                    break;
                case "younger" when age is null:
                    age = RelativeAge.Younger;
                    break;
                case var nth when position is null && nth.StartsWith("nth=", StringComparison.Ordinal):
                    position = ReadPosition(nth["nth=".Length..], sameGender: false);
                    break;
                case var nth when position is null && nth.StartsWith("nth-gender=", StringComparison.Ordinal):
                    position = ReadPosition(nth["nth-gender=".Length..], sameGender: true);
                    break;
                default:
                    throw new ArgumentException($"\"{part}\" in \"{text}\" is unknown or repeated.");
            }
        }
        return new KinshipStep(relation, gender, age, position);
    }

    private static ChildPosition ReadPosition(string text, bool sameGender) =>
        text == "youngest" ? ChildPosition.Last(sameGender)
        : int.TryParse(text, out var number) && number >= 1 ? ChildPosition.Nth(number, sameGender)
        : throw new ArgumentException($"\"{text}\" is not a position (1, 2, … or youngest).");

    private static Gender ReadGender(string text) => text switch
    {
        "male" => Gender.Male,
        "female" => Gender.Female,
        "other" => Gender.Other,
        _ => throw new ArgumentException($"\"{text}\" is not male, female or other."),
    };

    private sealed record CultureFile(
        string? Code,
        List<string>? Languages,
        Dictionary<string, string>? Name,
        Dictionary<string, string>? Fallback,
        List<RuleFile>? Rules);

    private sealed record RuleFile(string? Path, string? Start, Dictionary<string, string>? Label);
}
