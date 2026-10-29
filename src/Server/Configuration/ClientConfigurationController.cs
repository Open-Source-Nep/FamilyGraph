using Microsoft.AspNetCore.Mvc;
using Server.Accounts;
using Server.Localization;

namespace Server.Configuration;

/// <summary>
/// The instance settings the Client loads once when it starts, before anyone signs in. It carries
/// no secrets and nothing about the caller.
/// </summary>
[ApiController]
[Route("api/configuration")]
public sealed class ClientConfigurationController(GoogleSignInSettings google, TranslationCatalog translations) : ControllerBase
{
    [HttpGet]
    public ClientConfigurationResponse Get() => new(
        new GoogleSignInConfiguration(google.Enabled, google.Enabled ? google.ClientId : null),
        new LanguageConfiguration(translations.DefaultLanguage, translations.Languages));
}
