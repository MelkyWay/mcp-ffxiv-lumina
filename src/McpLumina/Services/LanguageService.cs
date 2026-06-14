using Lumina.Data;
using McpLumina.Constants;
using McpLumina.Models;

namespace McpLumina.Services;

/// <summary>
/// Handles language resolution, fallback chains, and Lumina Language mapping.
/// </summary>
public sealed class LanguageService
{
    private static readonly IReadOnlyList<string> FallbackChain = LanguageUtil.LanguageMap
            .Where(kvp => !string.IsNullOrEmpty(kvp.Value))
            .OrderBy(kvp => kvp.Value switch
            {
                // Order By Special Game Version Release Date
                "en" => 1, // For Global
                "chs" => 2, // For Chinese Simplified
                "ko" => 3, // For Korean
                "cht" => 4, // For Chinese Traditional
                "tc" => 5, // For Chinese Traditional
                _ => 100,
            })
            .Select(kvp => kvp.Value)
            .ToList();

    public static readonly Dictionary<string, Language> CodeToLumina = LanguageUtil.LanguageMap
        .Where(kvp => !string.IsNullOrEmpty(kvp.Value))
        .ToDictionary(x => x.Value, x => x.Key);

    private readonly HashSet<string> _available;
    private readonly string _defaultLanguage;

    public string DefaultLanguage => _defaultLanguage;

    public LanguageService(HashSet<string> availableLanguages, string defaultLanguage)
    {
        _available = availableLanguages;
        _defaultLanguage = defaultLanguage;
    }

    public IReadOnlyCollection<string> AvailableLanguages => _available;

    public string GetDisplayName(string code) =>
        CodeToLumina.TryGetValue(code, out var lang) ? lang.ToString() : code;

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

        var invalid = list.FirstOrDefault(lang => !CodeToLumina.ContainsKey(lang));
        if (invalid is not null)
            throw new LanguageUnavailableException(invalid);

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

    public static Language ToLuminaLanguage(string code) =>
        CodeToLumina.TryGetValue(code, out var lang)
            ? lang
            : throw new LanguageUnavailableException(code);

    public static Language? TryToLuminaLanguage(string code) =>
        CodeToLumina.TryGetValue(code, out var lang)
            ? lang
            : null;

    /// <summary>
    /// Resolves the effective default language given the configured value and the set of
    /// languages actually present in the local install.
    /// Falls back to "en" if the configured code is unavailable, then to the first available
    /// language if English is also absent.
    /// </summary>
    public static string ResolveEffectiveDefault(string configured, HashSet<string> available)
    {
        var code = configured.ToLowerInvariant();
        return available.Contains(code) ? code
             : available.Contains("en") ? "en"
             : available.FirstOrDefault() ?? "en";
    }

}
