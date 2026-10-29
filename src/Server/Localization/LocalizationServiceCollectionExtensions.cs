namespace Server.Localization;

public static class LocalizationServiceCollectionExtensions
{
    /// <summary>
    /// Reads the translation files next to the Server (<c>Translations/*.json</c>) and the default
    /// language (<c>Languages:Default</c>) once, at startup (D-047).
    /// </summary>
    public static IServiceCollection AddTranslations(this IServiceCollection services, IConfiguration configuration)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Translations");
        services.AddSingleton(TranslationCatalog.Load(directory, LanguageSettings.DefaultLanguage(configuration)));
        return services;
    }
}
