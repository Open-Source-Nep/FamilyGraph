namespace Server.Localization;

/// <summary>
/// One language's translations from <c>GET /api/translations/{code}</c>: each Client message ID
/// with its text, which the Client hands to <c>loadTranslations</c> before it starts (D-047).
/// </summary>
public sealed record TranslationsResponse(string Locale, IReadOnlyDictionary<string, string> Translations);
