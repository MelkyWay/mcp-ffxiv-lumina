using McpLumina.Constants;
using McpLumina.Models;
using Microsoft.Extensions.Logging;

namespace McpLumina.Configuration;

public sealed class ConfigValidator(ILogger<ConfigValidator> logger)
{

    /// <summary>
    /// Validates the configuration on startup and throws <see cref="ConfigException"/> with
    /// an actionable message if anything is wrong.
    /// </summary>
    public void ValidateOrThrow(ServerConfig config)
    {
        var errors = new List<string>();

        // gamePath
        if (string.IsNullOrWhiteSpace(config.GamePath))
        {
            errors.Add("gamePath is required. Set it in appsettings.json or via the McpLumina__GamePath environment variable.");
        }
        else
        {
            var resolved = Path.GetFullPath(config.GamePath);
            if (!Directory.Exists(resolved))
            {
                errors.Add($"gamePath '{resolved}' does not exist or is not a directory.");
            }
            else if (!IsLikelyFfxivRoot(resolved))
            {
                errors.Add($"gamePath '{resolved}' does not look like an FFXIV installation root (expected to find 'game/ffxivgame.ver' or 'sqpack' subdirectory).");
            }
        }

        // languageDefault
        if (!KnownLanguageCodes.Contains(config.LanguageDefault))
        {
            errors.Add($"languageDefault '{config.LanguageDefault}' is not valid. Allowed values: {string.Join(", ", KnownLanguageCodes.All)}.");
        }

        // cacheTTLSeconds
        if (config.CacheTTLSeconds < 0)
        {
            errors.Add("cacheTTLSeconds must be >= 0.");
        }

        if (errors.Count > 0)
        {
            var message = "mcp-lumina startup failed due to configuration errors:\n" +
                          string.Join("\n", errors.Select((e, i) => $"  {i + 1}. {e}"));
            throw new ConfigException(message);
        }

        logger.LogInformation("Configuration validated. GamePath={GamePath}, Language={Lang}, Cache={Cache}/{TTL}s",
            config.GamePath, config.LanguageDefault, config.CacheEnabled, config.CacheTTLSeconds);
    }

    private static bool IsLikelyFfxivRoot(string path)
    {
        return File.Exists(Path.Combine(path, "game", "ffxivgame.ver"))
            || Directory.Exists(Path.Combine(path, "sqpack"))
            || Directory.Exists(Path.Combine(path, "game", "sqpack"));
    }
}
