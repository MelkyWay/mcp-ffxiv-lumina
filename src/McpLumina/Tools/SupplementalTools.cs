using System.ComponentModel;
using McpLumina.Models.Responses;
using McpLumina.Services;
using McpLumina.Validators;
using ModelContextProtocol.Server;

namespace McpLumina.Tools;

/// <summary>
/// Tools backed by LuminaSupplemental.Excel — community-maintained CSV data that augments
/// the base FFXIV game sheets with drop tables, spawn positions, NPC locations, and more.
/// All supplemental data is loaded eagerly at startup from embedded CSV assets.
/// </summary>
[McpServerToolType]
public sealed class SupplementalTools(
    SupplementalDataService supplemental,
    GameDataService gameData)
{
    // ── get_mob_drops ─────────────────────────────────────────────────────

    [McpServerTool(Name = "get_mob_drops")]
    [Description(
        "Returns monster drop data from the community-maintained LuminaSupplemental dataset " +
        "(2,644 mob→item pairs). Each entry links a monster (BNpcNameId) to an item it can drop (ItemId). " +
        "Note: no drop rate or quantity data is available — pairs only. " +
        "Filter by monster name with monsterQuery, by item name with itemQuery, or omit both to list all. " +
        "Use limit and offset for pagination.")]
    public string GetMobDrops(
        [Description("Monster name substring to filter by (case-insensitive).")] string? monsterQuery = null,
        [Description("Item name substring to filter by (case-insensitive).")] string? itemQuery = null,
        [Description("Maximum number of results (1–200). Default 50.")] int? limit = null,
        [Description("Number of results to skip for pagination. Default 0.")] int? offset = null,
        [Description("Comma-separated language codes, e.g. 'en,ja'. Defaults to server default.")] string? languages = null) =>
        ToolHelper.Execute(() =>
        {
            var lim   = InputValidator.ValidateLimit(limit);
            var off   = InputValidator.ValidateOffset(offset);
            var langs = gameData.Languages.Resolve(InputValidator.ParseLanguages(languages));
            return ToolHelper.Ok(BuildMobDropsResponse(monsterQuery, itemQuery, lim, off, langs));
        });

    // ── Builder ───────────────────────────────────────────────────────────

    private MobDropsResponse BuildMobDropsResponse(
        string? monsterQuery, string? itemQuery,
        int limit, int offset, string[] langs)
    {
        var (returned, fallback) = gameData.Languages.ApplyFallback(langs);
        var primaryLang = returned.Contains("en") ? "en" : returned[0];

        var bnpcPrimary = supplemental.GetBNpcNames(primaryLang);
        var itemPrimary = supplemental.GetItemNames(primaryLang);

        var allMatches = new List<(uint BNpcNameId, string MobName, uint ItemId, string ItemName)>();

        foreach (var drop in supplemental.MobDrops)
        {
            if (!bnpcPrimary.TryGetValue(drop.BNpcNameId, out var mobName)) continue;
            if (!itemPrimary.TryGetValue(drop.ItemId, out var itemName)) continue;

            if (monsterQuery is not null &&
                !mobName.Contains(monsterQuery, StringComparison.OrdinalIgnoreCase)) continue;
            if (itemQuery is not null &&
                !itemName.Contains(itemQuery, StringComparison.OrdinalIgnoreCase)) continue;

            allMatches.Add((drop.BNpcNameId, mobName, drop.ItemId, itemName));
        }

        var totalMatches = allMatches.Count;
        var page         = allMatches.Skip(offset).Take(limit).ToList();

        // Build secondary-language name maps for only the paged rows
        var pageMonsterIds = new HashSet<uint>(page.Select(x => x.BNpcNameId));
        var pageItemIds    = new HashSet<uint>(page.Select(x => x.ItemId));

        var secMobNames = returned
            .Where(l => l != primaryLang)
            .ToDictionary(
                l => l,
                l =>
                {
                    var idx = supplemental.GetBNpcNames(l);
                    return pageMonsterIds
                        .Where(idx.ContainsKey)
                        .ToDictionary(id => id, id => idx[id]);
                });

        var secItemNames = returned
            .Where(l => l != primaryLang)
            .ToDictionary(
                l => l,
                l =>
                {
                    var idx = supplemental.GetItemNames(l);
                    return pageItemIds
                        .Where(idx.ContainsKey)
                        .ToDictionary(id => id, id => idx[id]);
                });

        var entries = page.Select(d =>
        {
            var mobNameMap  = new Dictionary<string, string> { [primaryLang] = d.MobName };
            var itemNameMap = new Dictionary<string, string> { [primaryLang] = d.ItemName };

            foreach (var (lang, idx) in secMobNames)
                if (idx.TryGetValue(d.BNpcNameId, out var n)) mobNameMap[lang]  = n;
            foreach (var (lang, idx) in secItemNames)
                if (idx.TryGetValue(d.ItemId, out var n))     itemNameMap[lang] = n;

            return new MobDropEntry(d.BNpcNameId, mobNameMap, d.ItemId, itemNameMap);
        }).ToArray();

        return new MobDropsResponse
        {
            MonsterQuery       = monsterQuery,
            ItemQuery          = itemQuery,
            LanguagesRequested = langs,
            LanguagesReturned  = returned,
            FallbackUsed       = fallback,
            TotalMatches       = totalMatches,
            Offset             = offset,
            Limit              = limit,
            Drops              = entries,
            GameVersion        = gameData.GameVersion,
            Timestamp          = DateTimeOffset.UtcNow.ToString("O"),
        };
    }
}
