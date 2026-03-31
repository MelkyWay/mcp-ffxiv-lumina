using System.ComponentModel;
using McpLumina.Models;
using McpLumina.Services;
using ModelContextProtocol.Server;

namespace McpLumina.Tools;

[McpServerToolType]
public sealed class HealthTool(GameDataService gameData, ResponseCacheService cache)
{
    [McpServerTool(Name = "health")]
    [Description(
        "Returns server health information including detected game version, validated version, " +
        "cache status, uptime, and any GameVersionMismatch warnings. " +
        "A 'degraded' status with a GameVersionMismatch warning means the game has been patched " +
        "since this server was last validated — generic tools still work, but FFXIV convenience " +
        "tools (get_jobs, get_duties, get_localized_labels) may return incorrect data.")]
    public string Health() =>
        ToolHelper.Execute(() => ToolHelper.Ok(gameData.GetHealth()));

    [McpServerTool(Name = "refresh_schema")]
    [Description(
        "Runs 'git fetch' + 'git checkout -B' in the configured EXDSchema directory to pull the " +
        "latest column name definitions, then clears the in-memory schema cache so subsequent " +
        "requests use the updated schema. Returns success/failure and a status message. " +
        "Call this when the health tool reports a SchemaOutdated warning.")]
    public string RefreshSchema()
    {
        var (success, message, errorCode) = gameData.RefreshSchema();
        if (!success) return ToolHelper.Err(errorCode, message);
        cache.InvalidateAll();
        return ToolHelper.Ok(new { refreshed = true, message });
    }

    [McpServerTool(Name = "list_languages")]
    [Description(
        "Returns the language codes globally available in this FFXIV installation (en/fr/de/ja). " +
        "Availability is detected by probing language-suffixed EXD files on disk. " +
        "Note: this is a global check — individual sheets may have a different language subset.")]
    public string ListLanguages() =>
        ToolHelper.Execute(() => ToolHelper.Ok(gameData.GetLanguages()));
}
