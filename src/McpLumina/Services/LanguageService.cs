using Lumina.Data;
using McpLumina.Constants;
using McpLumina.Models;

namespace McpLumina.Services;

/// <summary>
/// Handles language resolution, fallback chains, and Lumina Language mapping.
/// </summary>
public sealed class LanguageService
{
    private static readonly IReadOnlyList<string> FallbackChain = KnownLanguageCodes.All;

    private static readonly Dictionary<string, Language> CodeToLumina = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = Language.English,
        ["fr"] = Language.French,
        ["de"] = Language.German,
        ["ja"] = Language.Japanese,
    };

    private static readonly Dictionary<string, string> DisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "English",
        ["fr"] = "French",
        ["de"] = "German",
        ["ja"] = "Japanese",
    };

    private readonly HashSet<string> _available;
    private readonly string _defaultLanguage;

    public LanguageService(HashSet<string> availableLanguages, string defaultLanguage)
    {
        _available = availableLanguages;
        _defaultLanguage = defaultLanguage;
    }

    public IReadOnlyCollection<string> AvailableLanguages => _available;

    public string GetDisplayName(string code) =>
        DisplayNames.TryGetValue(code, out var name) ? name : code;

    /// <summary>
    /// Resolves a list of requested language codes.
    /// Null/empty → default language.
    /// Unknown codes → <see cref="LanguageUnavailableException"/>.
    /// </summary>
    public string[] Resolve(IEnumerable<string>? requested)
    {
        if (requested is null)
            return [_defaultLanguage];

        var list = requested.Select(l => l.ToLowerInvariant()).Distinct().ToArray();
        if (list.Length == 0)
            return [_defaultLanguage];

        foreach (var lang in list)
        {
            if (!CodeToLumina.ContainsKey(lang))
                throw new LanguageUnavailableException(lang);
        }

        return list;
    }

    /// <summary>
    /// For each requested language, returns the language actually used and whether a fallback occurred.
    /// E.g. if "fr" is unavailable, falls back to "en".
    /// </summary>
    public (string[] returned, bool fallbackUsed) ApplyFallback(string[] requested)
    {
        var returned = new List<string>();
        bool fallbackUsed = false;

        foreach (var lang in requested)
        {
            if (_available.Contains(lang))
            {
                returned.Add(lang);
            }
            else
            {
                // Walk fallback chain to find first available language
                var fallback = FallbackChain.FirstOrDefault(f => _available.Contains(f) && f != lang);
                if (fallback is not null)
                {
                    returned.Add(fallback);
                    fallbackUsed = true;
                }
                // If nothing available, skip – rare edge case
            }
        }

        // Deduplicate while preserving order
        return (returned.Distinct().ToArray(), fallbackUsed);
    }

    public Language ToLuminaLanguage(string code) =>
        CodeToLumina.TryGetValue(code, out var lang)
            ? lang
            : throw new LanguageUnavailableException(code);

    public static bool IsKnownCode(string code) =>
        CodeToLumina.ContainsKey(code.ToLowerInvariant());
}
