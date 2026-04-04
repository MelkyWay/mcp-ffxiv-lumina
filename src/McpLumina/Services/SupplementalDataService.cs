using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using LuminaSupplemental.Excel.Model;
using LuminaSupplemental.Excel.Services;
using Microsoft.Extensions.Logging;

namespace McpLumina.Services;

/// <summary>
/// Loads supplemental game data from LuminaSupplemental.Excel CSV assets at startup.
/// Pre-builds per-language name indices from Lumina sheets so tool queries can join
/// BNpcNameId/ItemId → display name without hitting the sheet on every call.
/// </summary>
public sealed class SupplementalDataService
{
    private readonly IReadOnlyList<MobDrop> _mobDrops;

    // lang → id → name
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<uint, string>> _bnpcNames;
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<uint, string>> _itemNames;

    public SupplementalDataService(
        GameDataService gameData,
        ILogger<SupplementalDataService> logger)
    {
        _mobDrops  = LoadMobDrops(gameData.Raw, logger);
        _bnpcNames = BuildNameIndices<BNpcName>(gameData, row => row.Singular.ToString(), logger, "BNpcName");
        _itemNames = BuildNameIndices<Item>(gameData, row => row.Name.ToString(), logger, "Item");

        logger.LogInformation(
            "SupplementalDataService ready. MobDrops={Drops}, BNpcName langs={Langs}",
            _mobDrops.Count,
            string.Join(",", _bnpcNames.Keys));
    }

    public IReadOnlyList<MobDrop> MobDrops => _mobDrops;

    public IReadOnlyDictionary<uint, string> GetBNpcNames(string lang) =>
        _bnpcNames.TryGetValue(lang, out var idx) ? idx
        : _bnpcNames.TryGetValue("en", out var en) ? en
        : new Dictionary<uint, string>();

    public IReadOnlyDictionary<uint, string> GetItemNames(string lang) =>
        _itemNames.TryGetValue(lang, out var idx) ? idx
        : _itemNames.TryGetValue("en", out var en) ? en
        : new Dictionary<uint, string>();

    // ── Private helpers ───────────────────────────────────────────────────

    private static IReadOnlyList<MobDrop> LoadMobDrops(
        Lumina.GameData gameData,
        ILogger logger)
    {
        var rows = CsvLoader.LoadResource<MobDrop>(
            CsvLoader.MobDropResourceName,
            includesHeaders: true,
            out _,
            out _,
            gameData,
            Language.English);

        logger.LogInformation("Loaded {Count} MobDrop rows from LuminaSupplemental", rows.Count);
        return rows.AsReadOnly();
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<uint, string>> BuildNameIndices<T>(
        GameDataService gameData,
        Func<T, string> nameSelector,
        ILogger logger,
        string sheetLabel)
        where T : struct, IExcelRow<T>
    {
        var result = new Dictionary<string, IReadOnlyDictionary<uint, string>>();

        foreach (var langCode in gameData.Languages.AvailableLanguages)
        {
            var lumLang = gameData.Languages.ToLuminaLanguage(langCode);
            var idx     = new Dictionary<uint, string>();

            try
            {
                foreach (var row in gameData.Raw.Excel.GetSheet<T>(lumLang))
                {
                    var name = nameSelector(row);
                    if (!string.IsNullOrWhiteSpace(name))
                        idx[row.RowId] = name;
                }
                result[langCode] = idx;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not build {Sheet} index for language {Lang}", sheetLabel, langCode);
            }
        }

        return result;
    }
}
