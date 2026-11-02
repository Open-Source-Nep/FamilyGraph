namespace Server.Localization;

public static class LocalizationServiceCollectionExtensions
{
    /// <summary>
    /// Reads the translation files next to the Server (<c>Translations/*.json</c>) and the default
    /// language (<c>Languages:Default</c>) once, at startup (D-047), then the culture files
    /// (<c>Cultures/*.json</c>) and the default culture (<c>Cultures:Default</c>) for relationship
    /// labels (D-048).
    /// </summary>
    public static IServiceCollection AddTranslations(this IServiceCollection services, IConfiguration configuration)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Translations");
        var translations = TranslationCatalog.Load(directory, LanguageSettings.DefaultLanguage(configuration));
        services.AddSingleton(translations);
        services.AddSingleton(CultureCatalog.Load(
            Path.Combine(AppContext.BaseDirectory, "Cultures"),
            CultureSettings.DefaultCulture(configuration),
            [.. translations.Languages.Select(language => language.Code)]));
        return services;
    }
}
