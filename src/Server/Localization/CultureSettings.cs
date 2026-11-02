namespace Server.Localization;

/// <summary>
/// The <c>Cultures</c> configuration: the culture whose relationship labels a browser starts with
/// until it remembers another choice (D-048). English when not set.
/// </summary>
public static class CultureSettings
{
    public const string Section = "Cultures";

    public const string English = "en";

    public static string DefaultCulture(IConfiguration configuration) =>
        configuration.GetSection(Section)["Default"] is { Length: > 0 } code ? code : English;
}
