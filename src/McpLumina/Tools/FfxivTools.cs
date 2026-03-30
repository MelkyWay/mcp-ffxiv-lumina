using System.ComponentModel;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using McpLumina.Constants;
using McpLumina.Models;
using McpLumina.Models.Responses;
using McpLumina.Services;
using McpLumina.Validators;
using ModelContextProtocol.Server;

namespace McpLumina.Tools;

/// <summary>
/// FFXIV-specific convenience tools that provide pre-shaped, role-aware data for
/// common game entities. Uses Lumina.Excel typed sheets (ClassJob, Item,
/// ContentFinderCondition) for correct, maintenance-free field access.
/// </summary>
[McpServerToolType]
public sealed class FfxivTools(GameDataService gameData, ResponseCacheService cache)
{
    // ── get_jobs ─────────────────────────────────────────────────────────

    [McpServerTool(Name = "get_jobs")]
    [Description(
        "Returns all jobs and base classes from the ClassJob sheet, enriched with derived " +
        "role groupings (Tank, Healer, Melee DPS, Physical Ranged DPS, Magical Ranged DPS, Limited). " +
        "Role labels are English-only in V1 (not sourced from game strings). " +
        "isJob=false indicates a base class (e.g. Marauder); isJob=true indicates a job (e.g. Warrior). " +
        "isLimited=true identifies Blue Mage.")]
    public string GetJobs(
        [Description("Comma-separated language codes for name/abbreviation fields, e.g. 'en,ja'. Defaults to server default.")] string? languages = null) =>
        ToolHelper.Execute(() =>
        {
            var langs    = gameData.Languages.Resolve(InputValidator.ParseLanguages(languages));
            var cacheKey = $"get_jobs:{string.Join(",", langs)}";
            return cache.GetOrCreate(cacheKey, () => ToolHelper.Ok(BuildJobsResponse(langs)));
        });

    // ── get_duties ───────────────────────────────────────────────────────

    [McpServerTool(Name = "get_duties")]
    [Description(
        "Returns duties from ContentFinderCondition, optionally filtered by category. " +
        "Category values: dungeon, trial, raid, ultimate, criterion, unreal. " +
        "ContentType IDs are hardcoded constants validated against a specific game version " +
        "(see ServerConstants.cs). A GameVersionMismatch warning from health() means these " +
        "filters should be re-verified. Omit category to return all duties.")]
    public string GetDuties(
        [Description("Optional category filter: dungeon | trial | raid | ultimate | criterion | unreal.")] string? category = null,
        [Description("Comma-separated language codes. Defaults to server default.")] string? languages = null) =>
        ToolHelper.Execute(() =>
        {
            InputValidator.ValidateDutyCategory(category);
            var langs    = gameData.Languages.Resolve(InputValidator.ParseLanguages(languages));
            var cacheKey = $"get_duties:{category ?? "all"}:{string.Join(",", langs)}";
            return cache.GetOrCreate(cacheKey, () => ToolHelper.Ok(BuildDutiesResponse(category, langs)));
        });

    // ── get_items ────────────────────────────────────────────────────────

    [McpServerTool(Name = "get_items")]
    [Description(
        "Searches the Item sheet by name and returns item data: item level, equip level, stack size, " +
        "icon ID, rarity (1=common/white, 2=green, 3=blue/rare, 4=purple/relic), filter group category, " +
        "NPC vendor price, and whether the item can be high quality. " +
        "Names are singular lowercase grammatical forms as stored in the game data (e.g. 'potion'). " +
        "Omit query to page through all named items. Use limit and offset for pagination.")]
    public string GetItems(
        [Description("Name substring to filter by (case-insensitive). Omit to return all items up to limit.")] string? query = null,
        [Description("Maximum number of results (1–200). Default 50.")] int? limit = null,
        [Description("Number of results to skip for pagination. Default 0.")] int? offset = null,
        [Description("Comma-separated language codes. Defaults to server default.")] string? languages = null) =>
        ToolHelper.Execute(() =>
        {
            var lim   = InputValidator.ValidateLimit(limit);
            var off   = InputValidator.ValidateOffset(offset);
            var langs = gameData.Languages.Resolve(InputValidator.ParseLanguages(languages));
            return ToolHelper.Ok(BuildItemsResponse(query, lim, off, langs));
        });

    // ── get_actions ──────────────────────────────────────────────────────

    [McpServerTool(Name = "get_actions")]
    [Description(
        "Searches player actions (abilities, spells, weaponskills) from the Action sheet. " +
        "Only returns IsPlayerAction=true rows by default, filtering out the ~47k NPC/system entries. " +
        "Use query to filter by name substring. Use classJobId to restrict to a specific job (ClassJob row ID). " +
        "ActionCategoryId: 2=Spell, 3=Weaponskill, 4=Ability. " +
        "Cast/recast times are in milliseconds. MaxCharges>1 indicates a charges-based action. " +
        "Use limit and offset for pagination.")]
    public string GetActions(
        [Description("Name substring to filter by (case-insensitive). Omit to return all player actions up to limit.")] string? query = null,
        [Description("Filter by ClassJob row ID, e.g. 25 for Black Mage. 0 = role/cross-class actions.")] int? classJobId = null,
        [Description("Maximum number of results (1–200). Default 50.")] int? limit = null,
        [Description("Number of results to skip for pagination. Default 0.")] int? offset = null,
        [Description("Comma-separated language codes. Defaults to server default.")] string? languages = null) =>
        ToolHelper.Execute(() =>
        {
            var lim       = InputValidator.ValidateLimit(limit);
            var off       = InputValidator.ValidateOffset(offset);
            var jobId     = InputValidator.ValidateClassJobId(classJobId);
            var langs     = gameData.Languages.Resolve(InputValidator.ParseLanguages(languages));
            return ToolHelper.Ok(BuildActionsResponse(query, jobId < 0 ? null : (uint)jobId, lim, off, langs));
        });

    // ── get_localized_labels ─────────────────────────────────────────────

    [McpServerTool(Name = "get_localized_labels")]
    [Description(
        "Returns localized label sets for well-known FFXIV enumerations. " +
        "kind values: jobs | roles | categories | ultimates | criterion | unreal. " +
        "- jobs: ClassJob names from the ClassJob sheet. " +
        "- roles: Derived English-only role grouping labels (not from game strings). " +
        "- categories: ContentType sheet names. " +
        "- ultimates/criterion/unreal: ContentFinderCondition rows filtered by ContentType ID.")]
    public string GetLocalizedLabels(
        [Description("Label kind: jobs | roles | categories | ultimates | criterion | unreal.")] string kind,
        [Description("Comma-separated language codes. Defaults to server default.")] string? languages = null) =>
        ToolHelper.Execute(() =>
        {
            InputValidator.ValidateLabelKind(kind);
            var langs    = gameData.Languages.Resolve(InputValidator.ParseLanguages(languages));
            var cacheKey = $"labels:{kind}:{string.Join(",", langs)}";
            return cache.GetOrCreate(cacheKey, () => ToolHelper.Ok(BuildLabelsResponse(kind, langs)));
        });

    // ── Private builders ─────────────────────────────────────────────────

    private JobsResponse BuildJobsResponse(string[] langs)
    {
        var (returned, fallback) = gameData.Languages.ApplyFallback(langs);

        // Load one typed sheet per language and pre-index all rows up front.
        // Pre-indexing avoids O(n²) GetRowOrDefault calls during the primary iteration.
        var langData = returned.ToDictionary(
            lang => lang,
            lang =>
            {
                var idx = new Dictionary<uint, (string name, string abbr)>();
                foreach (var row in GetSheet<ClassJob>(lang))
                {
                    if (row.RowId == 0) continue;
                    var n = row.Name.ToString();
                    if (!string.IsNullOrWhiteSpace(n))
                        idx[row.RowId] = (n, row.Abbreviation.ToString());
                }
                return idx;
            });

        var primaryLang = returned.Contains("en") ? "en" : returned[0];
        var entries     = new List<JobEntry>();

        foreach (var row in GetSheet<ClassJob>(primaryLang))
        {
            if (row.RowId == 0) continue;
            if (!langData[primaryLang].ContainsKey(row.RowId)) continue;

            var nameMap = new Dictionary<string, string>();
            var abbrMap = new Dictionary<string, string>();
            foreach (var (lang, idx) in langData)
            {
                if (idx.TryGetValue(row.RowId, out var data))
                {
                    nameMap[lang] = data.name;
                    abbrMap[lang] = data.abbr;
                }
            }

            entries.Add(new JobEntry(
                RowId:        row.RowId,
                Name:         nameMap,
                Abbreviation: abbrMap,
                Role:         RoleLabels.Resolve(row.Role, row.IsLimitedJob, row.RowId),
                IsJob:        row.JobIndex != 0,
                IsLimited:    row.IsLimitedJob,
                JobIndex:     row.JobIndex));
        }

        return new JobsResponse
        {
            LanguagesRequested = langs,
            LanguagesReturned  = returned,
            FallbackUsed       = fallback,
            Jobs               = [.. entries.OrderBy(e => e.JobIndex).ThenBy(e => e.RowId)],
            GameVersion        = gameData.GameVersion,
            Timestamp          = DateTimeOffset.UtcNow.ToString("O"),
        };
    }

    private ItemsResponse BuildItemsResponse(string? query, int limit, int offset, string[] langs)
    {
        var (returned, fallback) = gameData.Languages.ApplyFallback(langs);
        var primaryLang = returned.Contains("en") ? "en" : returned[0];

        // Pass 1: scan primary language sheet, filter by query, collect all fields.
        var allMatches = new List<(uint RowId, string Singular, string Display, string Desc,
                                   ushort ItemLevel, byte EquipLevel, uint StackSize,
                                   uint Icon, byte Rarity, byte FilterGroup, uint PriceMid, bool CanBeHq)>();

        foreach (var row in GetSheet<Item>(primaryLang))
        {
            if (row.RowId == 0) continue;
            var singular = row.Singular.ToString();
            if (string.IsNullOrWhiteSpace(singular)) continue;
            var display = row.Name.ToString();
            if (query is not null &&
                !singular.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !display.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
            allMatches.Add((
                row.RowId, singular, display,
                row.Description.ToString(),
                (ushort)row.LevelItem.RowId,
                row.LevelEquip,
                row.StackSize,
                row.Icon,
                row.Rarity,
                row.FilterGroup,
                row.PriceMid,
                row.CanBeHq));
        }

        var totalMatches = allMatches.Count;
        var page         = allMatches.Skip(offset).Take(limit).ToList();

        // Pass 2: read secondary language strings only for page rows.
        var pageRowIds = new HashSet<uint>(page.Select(x => x.RowId));
        var secondaryStrings = returned
            .Where(l => l != primaryLang)
            .ToDictionary(
                lang => lang,
                lang =>
                {
                    var idx = new Dictionary<uint, (string Singular, string Display, string Desc)>();
                    foreach (var row in GetSheet<Item>(lang))
                    {
                        if (!pageRowIds.Contains(row.RowId)) continue;
                        var s = row.Singular.ToString();
                        if (!string.IsNullOrWhiteSpace(s))
                            idx[row.RowId] = (s, row.Name.ToString(), row.Description.ToString());
                    }
                    return idx;
                });

        var entries = page.Select(match =>
        {
            var nameMap    = new Dictionary<string, string> { [primaryLang] = match.Singular };
            var displayMap = new Dictionary<string, string> { [primaryLang] = match.Display };
            var descMap    = new Dictionary<string, string> { [primaryLang] = match.Desc };
            foreach (var (lang, langIdx) in secondaryStrings)
            {
                if (!langIdx.TryGetValue(match.RowId, out var s)) continue;
                nameMap[lang]    = s.Singular;
                displayMap[lang] = s.Display;
                descMap[lang]    = s.Desc;
            }
            return new ItemEntry(
                RowId:       match.RowId,
                Name:        nameMap,
                DisplayName: displayMap,
                Description: descMap,
                ItemLevel:   match.ItemLevel,
                EquipLevel:  match.EquipLevel,
                StackSize:   match.StackSize,
                Icon:        match.Icon,
                Rarity:      match.Rarity,
                FilterGroup: match.FilterGroup,
                PriceMid:    match.PriceMid,
                CanBeHq:     match.CanBeHq);
        }).ToArray();

        return new ItemsResponse
        {
            Query              = query ?? string.Empty,
            LanguagesRequested = langs,
            LanguagesReturned  = returned,
            FallbackUsed       = fallback,
            TotalMatches       = totalMatches,
            Offset             = offset,
            Limit              = limit,
            Items              = entries,
            GameVersion        = gameData.GameVersion,
            Timestamp          = DateTimeOffset.UtcNow.ToString("O"),
        };
    }

    private DutiesResponse BuildDutiesResponse(string? category, string[] langs)
    {
        var (returned, fallback) = gameData.Languages.ApplyFallback(langs);
        int? filterCtId = category is null ? null : DutyCategories.ToContentTypeId(category);

        // Pre-index names per language.
        var langNames = returned.ToDictionary(
            lang => lang,
            lang =>
            {
                var idx = new Dictionary<uint, string>();
                foreach (var row in GetSheet<ContentFinderCondition>(lang))
                {
                    if (row.RowId == 0) continue;
                    var n = row.Name.ToString();
                    if (!string.IsNullOrWhiteSpace(n)) idx[row.RowId] = n;
                }
                return idx;
            });

        var primaryLang = returned.Contains("en") ? "en" : returned[0];
        var primary     = GetSheet<ContentFinderCondition>(primaryLang);
        var entries     = new List<DutyEntry>();

        foreach (var row in primary)
        {
            if (row.RowId == 0) continue;

            var ctId = (int)row.ContentType.RowId;
            if (filterCtId.HasValue && ctId != filterCtId.Value) continue;

            var enName = langNames.TryGetValue("en", out var enIdx) && enIdx.TryGetValue(row.RowId, out var n)
                ? n : row.Name.ToString();

            if (string.Equals(category, DutyCategories.Unreal, StringComparison.OrdinalIgnoreCase) &&
                !enName.Contains(DutyCategories.UnrealNameSuffix, StringComparison.OrdinalIgnoreCase))
                continue;

            var resolvedCategory = ResolveCategory(ctId, enName);
            if (resolvedCategory is null || string.IsNullOrWhiteSpace(enName)) continue;

            var nameMap = new Dictionary<string, string>();
            foreach (var (lang, langIdx) in langNames)
            {
                if (langIdx.TryGetValue(row.RowId, out var langName))
                    nameMap[lang] = langName;
            }

            entries.Add(new DutyEntry(
                RowId:             row.RowId,
                Name:              nameMap,
                Category:          resolvedCategory,
                IsHighEndDuty:     row.HighEndDuty,
                LevelRequired:     row.ClassJobLevelRequired,
                ItemLevelRequired: row.ItemLevelRequired));
        }

        return new DutiesResponse
        {
            Category           = category,
            LanguagesRequested = langs,
            LanguagesReturned  = returned,
            FallbackUsed       = fallback,
            Count              = entries.Count,
            Duties             = [.. entries],
            GameVersion        = gameData.GameVersion,
            Timestamp          = DateTimeOffset.UtcNow.ToString("O"),
        };
    }

    private LabelsResponse BuildLabelsResponse(string kind, string[] langs)
    {
        var (returned, fallback) = gameData.Languages.ApplyFallback(langs);

        var labels = kind.ToLowerInvariant() switch
        {
            "jobs"       => GetJobLabels(returned),
            "roles"      => GetRoleLabels(),
            "categories" => GetCategoryLabels(returned),
            "ultimates"  => GetDutyLabels(ContentTypeIds.Ultimate, null, returned),
            "criterion"  => GetDutyLabels(ContentTypeIds.Criterion, null, returned),
            "unreal"     => GetDutyLabels(ContentTypeIds.Trial, DutyCategories.UnrealNameSuffix, returned),
            _            => throw new ValidationException($"Unknown kind '{kind}'."),
        };

        return new LabelsResponse
        {
            Kind               = kind,
            LanguagesRequested = langs,
            LanguagesReturned  = returned,
            FallbackUsed       = fallback,
            Labels             = [.. labels],
            GameVersion        = gameData.GameVersion,
            Timestamp          = DateTimeOffset.UtcNow.ToString("O"),
        };
    }

    private List<LabelEntry> GetJobLabels(string[] langs)
    {
        var langData = langs.ToDictionary(
            lang => lang,
            lang =>
            {
                var idx = new Dictionary<uint, string>();
                foreach (var row in GetSheet<ClassJob>(lang))
                {
                    if (row.RowId == 0) continue;
                    var n = row.Name.ToString();
                    if (!string.IsNullOrWhiteSpace(n)) idx[row.RowId] = n;
                }
                return idx;
            });

        var primaryLang = langs.Contains("en") ? "en" : langs[0];
        var result      = new List<LabelEntry>();

        foreach (var row in GetSheet<ClassJob>(primaryLang))
        {
            if (row.RowId == 0) continue;
            if (!langData[primaryLang].ContainsKey(row.RowId)) continue;

            var nameMap = new Dictionary<string, string>();
            foreach (var (lang, idx) in langData)
            {
                if (idx.TryGetValue(row.RowId, out var name))
                    nameMap[lang] = name;
            }
            result.Add(new LabelEntry(row.RowId, nameMap));
        }
        return result;
    }

    private static List<LabelEntry> GetRoleLabels() =>
    [
        .. RoleLabels.ByRoleId.Select(kv => new LabelEntry(
            RowId: kv.Key,
            Name:  new Dictionary<string, string> { ["en"] = kv.Value },
            Note:  "derived — not sourced from game strings")),
        new LabelEntry(
            RowId: 99,
            Name:  new Dictionary<string, string> { ["en"] = RoleLabels.Limited },
            Note:  "derived — not sourced from game strings"),
    ];

    private List<LabelEntry> GetCategoryLabels(string[] langs)
    {
        var primaryLang = langs.Contains("en") ? "en" : langs[0];
        var result      = new List<LabelEntry>();

        foreach (var row in GetSheet<Lumina.Excel.Sheets.ContentType>(primaryLang))
        {
            if (row.RowId == 0) continue;
            var name = row.Name.ToString();
            if (string.IsNullOrWhiteSpace(name)) continue;

            var nameMap = new Dictionary<string, string>();
            foreach (var lang in langs)
            {
                var r = GetSheet<Lumina.Excel.Sheets.ContentType>(lang).GetRowOrDefault(row.RowId);
                nameMap[lang] = r?.Name.ToString() ?? string.Empty;
            }
            result.Add(new LabelEntry(row.RowId, nameMap));
        }
        return result;
    }

    private List<LabelEntry> GetDutyLabels(int contentTypeId, string? nameSuffix, string[] langs)
    {
        var primaryLang = langs.Contains("en") ? "en" : langs[0];
        var result      = new List<LabelEntry>();

        foreach (var row in GetSheet<ContentFinderCondition>(primaryLang))
        {
            if (row.RowId == 0) continue;
            if ((int)row.ContentType.RowId != contentTypeId) continue;

            var name = row.Name.ToString();
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (nameSuffix is not null && !name.Contains(nameSuffix, StringComparison.OrdinalIgnoreCase)) continue;

            var nameMap = new Dictionary<string, string>();
            foreach (var lang in langs)
            {
                var r = GetSheet<ContentFinderCondition>(lang).GetRowOrDefault(row.RowId);
                nameMap[lang] = r?.Name.ToString() ?? string.Empty;
            }
            result.Add(new LabelEntry(row.RowId, nameMap));
        }
        return result;
    }

    private ActionsResponse BuildActionsResponse(string? query, uint? classJobId, int limit, int offset, string[] langs)
    {
        var (returned, fallback) = gameData.Languages.ApplyFallback(langs);
        var primaryLang = returned.Contains("en") ? "en" : returned[0];

        // Build ActionCategory name lookup (small sheet, all langs).
        var categoryNames = new Dictionary<uint, string>();
        foreach (var cat in GetSheet<ActionCategory>(primaryLang))
        {
            var n = cat.Name.ToString();
            if (!string.IsNullOrWhiteSpace(n)) categoryNames[cat.RowId] = n;
        }

        // Pass 1: scan primary language, apply filters, collect all fields.
        var allMatches = new List<(uint RowId, string Name, uint Icon, uint ClassJobId,
                                   byte Level, uint CategoryId, bool IsRole, bool IsPvP,
                                   uint CastMs, uint RecastMs, byte MaxCharges)>();

        foreach (var row in GetSheet<Lumina.Excel.Sheets.Action>(primaryLang))
        {
            if (!row.IsPlayerAction) continue;
            var name = row.Name.ToString();
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (classJobId.HasValue && row.ClassJob.RowId != classJobId.Value) continue;
            if (query is not null && !name.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;

            allMatches.Add((
                row.RowId,
                name,
                row.Icon,
                row.ClassJob.RowId,
                row.ClassJobLevel,
                row.ActionCategory.RowId,
                row.IsRoleAction,
                row.IsPvP,
                (uint)row.Cast100ms   * 100,
                (uint)row.Recast100ms * 100,
                row.MaxCharges));
        }

        var totalMatches = allMatches.Count;
        var page         = allMatches.Skip(offset).Take(limit).ToList();

        // Pass 2: secondary language names for page rows only.
        var pageRowIds = new HashSet<uint>(page.Select(x => x.RowId));
        var secondaryNames = returned
            .Where(l => l != primaryLang)
            .ToDictionary(
                lang => lang,
                lang =>
                {
                    var idx = new Dictionary<uint, string>();
                    foreach (var row in GetSheet<Lumina.Excel.Sheets.Action>(lang))
                    {
                        if (!pageRowIds.Contains(row.RowId)) continue;
                        var n = row.Name.ToString();
                        if (!string.IsNullOrWhiteSpace(n)) idx[row.RowId] = n;
                    }
                    return idx;
                });

        var entries = page.Select(m =>
        {
            var nameMap = new Dictionary<string, string> { [primaryLang] = m.Name };
            foreach (var (lang, idx) in secondaryNames)
                if (idx.TryGetValue(m.RowId, out var n)) nameMap[lang] = n;

            return new ActionEntry(
                RowId:              m.RowId,
                Name:               nameMap,
                Icon:               m.Icon,
                ClassJobId:         m.ClassJobId,
                ClassJobLevel:      m.Level,
                ActionCategoryId:   m.CategoryId,
                ActionCategoryName: categoryNames.GetValueOrDefault(m.CategoryId, string.Empty),
                IsRoleAction:       m.IsRole,
                IsPvP:              m.IsPvP,
                CastTimeMs:         m.CastMs,
                RecastTimeMs:       m.RecastMs,
                MaxCharges:         m.MaxCharges);
        }).ToArray();

        return new ActionsResponse
        {
            Query              = query,
            LanguagesRequested = langs,
            LanguagesReturned  = returned,
            FallbackUsed       = fallback,
            TotalMatches       = totalMatches,
            Offset             = offset,
            Limit              = limit,
            Actions            = entries,
            GameVersion        = gameData.GameVersion,
            Timestamp          = DateTimeOffset.UtcNow.ToString("O"),
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private ExcelSheet<T> GetSheet<T>(string lang) where T : struct, IExcelRow<T> =>
        gameData.Raw.Excel.GetSheet<T>(gameData.Languages.ToLuminaLanguage(lang));

    private static string? ResolveCategory(int contentTypeId, string name) => contentTypeId switch
    {
        ContentTypeIds.Dungeon   => DutyCategories.Dungeon,
        ContentTypeIds.Trial     => name.Contains(DutyCategories.UnrealNameSuffix, StringComparison.OrdinalIgnoreCase)
                                        ? DutyCategories.Unreal
                                        : DutyCategories.Trial,
        ContentTypeIds.Raid      => DutyCategories.Raid,
        ContentTypeIds.Ultimate  => DutyCategories.Ultimate,
        ContentTypeIds.Criterion => DutyCategories.Criterion,
        _ => null,
    };
}
