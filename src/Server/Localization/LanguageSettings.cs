namespace Server.Localization;

/// <summary>
/// The <c>Languages</c> configuration: the language the Client starts in until a browser remembers
/// another choice (D-047). English when not set.
/// </summary>
public static class LanguageSettings
{
    public const string Section = "Languages";

    public static string DefaultLanguage(IConfiguration configuration) =>
        configuration.GetSection(Section)["Default"] is { Length: > 0 } code ? code : TranslationCatalog.SourceLanguage;
}
