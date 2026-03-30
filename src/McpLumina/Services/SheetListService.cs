using Lumina;
using Microsoft.Extensions.Logging;

namespace McpLumina.Services;

/// <summary>
/// Returns all available sheet names from the game data via ExcelModule.GetSheetNames().
/// The result is cached for the server lifetime — the sheet list doesn't change at runtime.
/// </summary>
public sealed class SheetListService(GameData gameData, ILogger<SheetListService> logger)
{
    // Lazy<T> gives thread-safe, once-only initialisation without explicit locking.
    private readonly Lazy<string[]> _names = new(() =>
    {
        var names  = gameData.Excel.SheetNames;
        var result = names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();
        logger.LogInformation("Sheet list loaded: {Count} sheets.", result.Length);
        return result;
    });

    public string[] GetSheetNames() => _names.Value;
}
