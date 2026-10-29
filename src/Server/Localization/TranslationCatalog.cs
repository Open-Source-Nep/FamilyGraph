using System.Security.Cryptography;
using System.Text.Json;

namespace Server.Localization;

/// <summary>
/// The languages this instance offers and their translations of the Client's texts (D-047). The
/// Client is written in English, which needs no file. Each other language is a JSON file in
/// <c>Translations</c>, read once at startup: <c>{ "locale", "name", "translations" }</c>, where
/// <c>translations</c> maps each message ID to its text.
/// </summary>
public sealed class TranslationCatalog
{
    /// <summary>The language the Client is written in.</summary>
    public const string SourceLanguage = "en";

    private static readonly JsonSerializerOptions FileOptions = new(JsonSerializerDefaults.Web);

    private readonly Dictionary<string, Translations> translations;

    private TranslationCatalog(string defaultLanguage, IReadOnlyList<Language> languages, Dictionary<string, Translations> translations)
    {
        DefaultLanguage = defaultLanguage;
        Languages = languages;
        this.translations = translations;
    }

    /// <summary>The language a browser without a remembered choice starts in.</summary>
    public string DefaultLanguage { get; }

    /// <summary>English first, then the translated languages by code.</summary>
    public IReadOnlyList<Language> Languages { get; }

    /// <summary>A translated language's file as the Client loads it; English has none.</summary>
    public Translations? Find(string code) => translations.GetValueOrDefault(code);

    /// <summary>
    /// Reads every <c>*.json</c> file in <paramref name="directory"/>. A file that cannot be read,
    /// whose <c>locale</c> is not its file name, or a default language that has no file stops the
    /// startup, so that a broken deploy is noticed at once.
    /// </summary>
    public static TranslationCatalog Load(string directory, string defaultLanguage)
    {
        var loaded = new Dictionary<string, Translations>(StringComparer.Ordinal);
        var languages = new List<Language> { new(SourceLanguage, "English") };
        var files = Directory.Exists(directory) ? Directory.GetFiles(directory, "*.json") : [];
        foreach (var path in files.Order(StringComparer.Ordinal))
        {
            var code = Path.GetFileNameWithoutExtension(path);
            var (locale, name, messages) = Read(path);
            if (locale != code || code == SourceLanguage)
            {
                throw new InvalidOperationException($"The translation file {path} must name the language {code} in \"locale\", and English needs no file.");
            }
            var content = JsonSerializer.SerializeToUtf8Bytes(new TranslationsResponse(code, messages), FileOptions);
            loaded[code] = new Translations(content, $"\"{Convert.ToHexStringLower(SHA256.HashData(content))[..16]}\"");
            languages.Add(new Language(code, name));
        }

        if (defaultLanguage != SourceLanguage && !loaded.ContainsKey(defaultLanguage))
        {
            throw new InvalidOperationException($"{LanguageSettings.Section}:Default is \"{defaultLanguage}\", but there is no translation file for it.");
        }
        return new TranslationCatalog(defaultLanguage, languages, loaded);
    }

    private static (string Locale, string Name, Dictionary<string, string> Translations) Read(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var file = JsonSerializer.Deserialize<TranslationFile>(stream, FileOptions);
            if (file is { Locale.Length: > 0, Name.Length: > 0, Translations: { } translations }
                && translations.Values.All(text => text is not null))
            {
                return (file.Locale, file.Name, translations);
            }
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"The translation file {path} is not valid JSON.", exception);
        }
        throw new InvalidOperationException($"The translation file {path} needs \"locale\", \"name\" and \"translations\".");
    }

    private sealed record TranslationFile(string? Locale, string? Name, Dictionary<string, string>? Translations);
}

/// <summary>A language the Client can be shown in, named in that language, such as "नेपाली".</summary>
public sealed record Language(string Code, string Name);

/// <summary>One language's response body, serialized once, and its ETag.</summary>
public sealed record Translations(byte[] Content, string ETag);
