using System.Text.Json;
using System.Text.RegularExpressions;

namespace Server.IntegrationTests.Localization;

/// <summary>
/// Every translation file covers exactly the Client's messages, as extracted to
/// <c>src/client/src/locale/messages.json</c> by <c>npm run extract-i18n</c> (D-047), and keeps
/// each message's placeholders.
/// </summary>
public partial class TranslationFileTests
{
    [Theory]
    [Trait("AC", "AC-09.13")]
    [InlineData("ne")]
    public void Translates_every_Client_message_with_the_same_placeholders(string code)
    {
        var root = RepositoryRoot();
        var source = Messages(Path.Combine(root, "src", "client", "src", "locale", "messages.json"));
        var translated = Messages(Path.Combine(root, "src", "Server", "Translations", $"{code}.json"));

        Assert.Empty(source.Keys.Except(translated.Keys).Order());
        Assert.Empty(translated.Keys.Except(source.Keys).Order());
        var mismatched = source.Keys
            .Where(id => !Placeholders(source[id]).SequenceEqual(Placeholders(translated[id])))
            .Order();
        Assert.Empty(mismatched);
    }

    private static Dictionary<string, string> Messages(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("translations").EnumerateObject()
            .ToDictionary(message => message.Name, message => message.Value.GetString()!);
    }

    /// <summary>The <c>{$NAME}</c> placeholders in any order, and the ICU ones such as <c>{VAR_PLURAL, plural, …}</c>.</summary>
    private static IEnumerable<string> Placeholders(string text) =>
        Placeholder().Matches(text).Select(match => match.Value).Order();

    [GeneratedRegex(@"\{\$[A-Za-z0-9_]+\}|\{VAR_[A-Z]+|\{INTERPOLATION[0-9_]*\}")]
    private static partial Regex Placeholder();

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FamilyGraph.slnx")))
            {
                return directory.FullName;
            }
        }
        throw new InvalidOperationException("The repository root (FamilyGraph.slnx) was not found.");
    }
}
