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

    // ── get_mounts ───────────────────────────────────────────────────────

    [McpServerTool(Name = "get_mounts")]
    [Description(
        "Searches mounts from the Mount sheet. " +
        "Use query to filter by name substring. " +
        "isFlying indicates the mount can fly. " +
        "extraSeats > 0 indicates a multi-seat mount. " +
        "Use limit and offset for pagination.")]
    public string GetMounts(
        [Description("Name substring to filter by (case-insensitive). Omit to return all mounts up to limit.")] string? query = null,
        [Description("Maximum number of results (1–200). Default 50.")] int? limit = null,
        [Description("Number of results to skip for pagination. Default 0.")] int? offset = null,
        [Description("Comma-separated language codes. Defaults to server default.")] string? languages = null) =>
        ToolHelper.Execute(() =>
        {
            var lim   = InputValidator.ValidateLimit(limit);
            var off   = InputValidator.ValidateOffset(offset);
            var langs = gameData.Languages.Resolve(InputValidator.ParseLanguages(languages));
            return ToolHelper.Ok(BuildMountsResponse(query, lim, off, langs));
        });

    // ── get_minions ──────────────────────────────────────────────────────

    [McpServerTool(Name = "get_minions")]
    [Description(
        "Searches minions (companions) from the Companion sheet. " +
        "Use query to filter by name substring. " +
        "Use limit and offset for pagination.")]
    public string GetMinions(
        [Description("Name substring to filter by (case-insensitive). Omit to return all minions up to limit.")] string? query = null,
        [Description("Maximum number of results (1–200). Default 50.")] int? limit = null,
        [Description("Number of results to skip for pagination. Default 0.")] int? offset = null,
        [Description("Comma-separated language codes. Defaults to server default.")] string? languages = null) =>
        ToolHelper.Execute(() =>
        {
            var lim   = InputValidator.ValidateLimit(limit);
            var off   = InputValidator.ValidateOffset(offset);
            var langs = gameData.Languages.Resolve(InputValidator.ParseLanguages(languages));
            return ToolHelper.Ok(BuildMinionsResponse(query, lim, off, langs));
        });

    // ── get_achievements ─────────────────────────────────────────────────

    [McpServerTool(Name = "get_achievements")]
    [Description(
        "Searches achievements from the Achievement sheet. " +
        "Use query to filter by name substring. " +
        "Points reflects the achievement's point value. " +
        "Use limit and offset for pagination.")]
    public string GetAchievements(
        [Description("Name substring to filter by (case-insensitive). Omit to return all achievements up to limit.")] string? query = null,
        [Description("Maximum number of results (1–200). Default 50.")] int? limit = null,
        [Description("Number of results to skip for pagination. Default 0.")] int? offset = null,
        [Description("Comma-separated language codes. Defaults to server default.")] string? languages = null) =>
        ToolHelper.Execute(() =>
        {
            var lim   = InputValidator.ValidateLimit(limit);
            var off   = InputValidator.ValidateOffset(offset);
            var langs = gameData.Languages.Resolve(InputValidator.ParseLanguages(languages));
            return ToolHelper.Ok(BuildAchievementsResponse(query, lim, off, langs));
        });

    // ── get_traits ───────────────────────────────────────────────────────

    [McpServerTool(Name = "get_traits")]
    [Description(
        "Returns passive job traits from the Trait sheet. " +
        "Use classJobId to filter by job (ClassJob row ID, e.g. 24 for White Mage). " +
        "Use query to filter by name substring. " +
        "Results are ordered by level. Use limit and offset for pagination.")]
    public string GetTraits(
        [Description("Name substring to filter by (case-insensitive). Omit to return all traits up to limit.")] string? query = null,
        [Description("Filter by ClassJob row ID, e.g. 24 for White Mage.")] int? classJobId = null,
        [Description("Maximum number of results (1–200). Default 50.")] int? limit = null,
        [Description("Number of results to skip for pagination. Default 0.")] int? offset = null,
        [Description("Comma-separated language codes. Defaults to server default.")] string? languages = null) =>
        ToolHelper.Execute(() =>
        {
            var lim   = InputValidator.ValidateLimit(limit);
            var off   = InputValidator.ValidateOffset(offset);
            var jobId = InputValidator.ValidateClassJobId(classJobId);
            var langs = gameData.Languages.Resolve(InputValidator.ParseLanguages(languages));
            return ToolHelper.Ok(BuildTraitsResponse(query, jobId < 0 ? null : (uint)jobId, lim, off, langs));
        });

    // ── get_statuses ─────────────────────────────────────────────────────

    [McpServerTool(Name = "get_statuses")]
    [Description(
        "Searches status effects (buffs and debuffs) from the Status sheet. " +
        "Use query to filter by name substring. Use category to filter by type: " +
        "'beneficial' (buffs), 'detrimental' (debuffs), or omit for all. " +
        "StatusCategory: 1=Detrimental, 2=Beneficial. " +
        "MaxStacks=0 means the status is not stackable. " +
        "Use limit and offset for pagination.")]
    public string GetStatuses(
        [Description("Name substring to filter by (case-insensitive). Omit to return all named statuses up to limit.")] string? query = null,
        [Description("Category filter: beneficial | detrimental. Omit for all.")] string? category = null,
        [Description("Maximum number of results (1–200). Default 50.")] int? limit = null,
        [Description("Number of results to skip for pagination. Default 0.")] int? offset = null,
        [Description("Comma-separated language codes. Defaults to server default.")] string? languages = null) =>
        ToolHelper.Execute(() =>
        {
            InputValidator.ValidateStatusCategory(category);
            var lim   = InputValidator.ValidateLimit(limit);
            var off   = InputValidator.ValidateOffset(offset);
            var langs = gameData.Languages.Resolve(InputValidator.ParseLanguages(languages));
            return ToolHelper.Ok(BuildStatusesResponse(query, category, lim, off, langs));
        });

    // ── get_races ─────────────────────────────────────────────────────────

    [McpServerTool(Name = "get_races")]
    [Description(
        "Returns all playable races from the Race sheet. " +
        "Each race has a masculine and feminine name (may differ depending on language). " +
        "Row IDs: 1=Hyur, 2=Elezen, 3=Lalafell, 4=Miqo'te, 5=Roegadyn, 6=Au Ra, 7=Hrothgar, 8=Viera.")]
    public string GetRaces(
        [Description("Comma-separated language codes. Defaults to server default.")] string? languages = null) =>
        ToolHelper.Execute(() =>
        {
            var langs    = gameData.Languages.Resolve(InputValidator.ParseLanguages(languages));
            var cacheKey = $"get_races:{string.Join(",", langs)}";
            return cache.GetOrCreate(cacheKey, () => ToolHelper.Ok(BuildRacesResponse(langs)));
        });

    // ── get_worlds ────────────────────────────────────────────────────────

    [McpServerTool(Name = "get_worlds")]
    [Description(
        "Returns game worlds (servers) from the World sheet. " +
        "Only public player-accessible worlds are returned (IsPublic=true). " +
        "DataCenterName reflects the data centre the world belongs to. " +
        "Use query to filter by world name substring. " +
        "World names are English-only (proper nouns, same across all languages).")]
    public string GetWorlds(
        [Description("World name substring to filter by (case-insensitive). Omit to return all public worlds.")] string? query = null) =>
        ToolHelper.Execute(() =>
        {
            var cacheKey = $"get_worlds:{query ?? "all"}";
            return cache.GetOrCreate(cacheKey, () => ToolHelper.Ok(BuildWorldsResponse(query)));
        });

    // ── get_weather ───────────────────────────────────────────────────────

    [McpServerTool(Name = "get_weather")]
    [Description(
        "Searches weather types from the Weather sheet. " +
        "Use query to filter by name substring. " +
        "Use limit and offset for pagination.")]
    public string GetWeather(
        [Description("Name substring to filter by (case-insensitive). Omit to return all weather types up to limit.")] string? query = null,
        [Description("Maximum number of results (1–200). Default 50.")] int? limit = null,
        [Description("Number of results to skip for pagination. Default 0.")] int? offset = null,
        [Description("Comma-separated language codes. Defaults to server default.")] string? languages = null) =>
        ToolHelper.Execute(() =>
        {
            var lim   = InputValidator.ValidateLimit(limit);
            var off   = InputValidator.ValidateOffset(offset);
            var langs = gameData.Languages.Resolve(InputValidator.ParseLanguages(languages));
            return ToolHelper.Ok(BuildWeatherResponse(query, lim, off, langs));
        });

    // ── get_titles ────────────────────────────────────────────────────────

    [McpServerTool(Name = "get_titles")]
    [Description(
        "Searches player titles from the Title sheet. " +
        "Each title has a masculine and feminine form. " +
        "IsPrefix=true means the title appears before the character name; false means after. " +
        "Use query to filter by title text substring. " +
        "Use limit and offset for pagination.")]
    public string GetTitles(
        [Description("Title text substring to filter by (case-insensitive, matches masculine or feminine form). Omit to return all titles up to limit.")] string? query = null,
        [Description("Maximum number of results (1–200). Default 50.")] int? limit = null,
        [Description("Number of results to skip for pagination. Default 0.")] int? offset = null,
        [Description("Comma-separated language codes. Defaults to server default.")] string? languages = null) =>
        ToolHelper.Execute(() =>
        {
            var lim   = InputValidator.ValidateLimit(limit);
            var off   = InputValidator.ValidateOffset(offset);
            var langs = gameData.Languages.Resolve(InputValidator.ParseLanguages(languages));
            return ToolHelper.Ok(BuildTitlesResponse(query, lim, off, langs));
        });

    // ── get_currencies ────────────────────────────────────────────────────

    [McpServerTool(Name = "get_currencies")]
    [Description(
        "Returns in-game currencies (Gil, tomestones, seals, etc.) from the Item sheet. " +
        "Currencies are identified by ItemUICategory=63, FilterGroup=16, StackSize>1. " +
        "StackSize indicates the maximum a player can hold (e.g. 999999999 for Gil, 2000 for tomestones).")]
    public string GetCurrencies(
        [Description("Comma-separated language codes. Defaults to server default.")] string? languages = null) =>
        ToolHelper.Execute(() =>
        {
            var langs    = gameData.Languages.Resolve(InputValidator.ParseLanguages(languages));
            var cacheKey = $"get_currencies:{string.Join(",", langs)}";
            return cache.GetOrCreate(cacheKey, () => ToolHelper.Ok(BuildCurrenciesResponse(langs)));
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

    private MountsResponse BuildMountsResponse(string? query, int limit, int offset, string[] langs)
    {
        var (returned, fallback) = gameData.Languages.ApplyFallback(langs);
        var primaryLang = returned.Contains("en") ? "en" : returned[0];

        var allMatches = new List<(uint RowId, string Name, uint Icon, byte IsFlying, byte ExtraSeats)>();

        foreach (var row in GetSheet<Mount>(primaryLang))
        {
            var name = row.Singular.ToString();
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (query is not null && !name.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;

            allMatches.Add((row.RowId, name, (uint)row.Icon, row.IsFlying, row.ExtraSeats));
        }

        var totalMatches = allMatches.Count;
        var page         = allMatches.Skip(offset).Take(limit).ToList();

        var pageRowIds = new HashSet<uint>(page.Select(x => x.RowId));
        var secondaryNames = returned
            .Where(l => l != primaryLang)
            .ToDictionary(
                lang => lang,
                lang =>
                {
                    var idx = new Dictionary<uint, string>();
                    foreach (var row in GetSheet<Mount>(lang))
                    {
                        if (!pageRowIds.Contains(row.RowId)) continue;
                        var n = row.Singular.ToString();
                        if (!string.IsNullOrWhiteSpace(n)) idx[row.RowId] = n;
                    }
                    return idx;
                });

        var entries = page.Select(m =>
        {
            var nameMap = new Dictionary<string, string> { [primaryLang] = m.Name };
            foreach (var (lang, idx) in secondaryNames)
                if (idx.TryGetValue(m.RowId, out var n)) nameMap[lang] = n;

            return new MountEntry(m.RowId, nameMap, m.Icon, m.IsFlying != 0, m.ExtraSeats);
        }).ToArray();

        return new MountsResponse
        {
            Query              = query,
            LanguagesRequested = langs,
            LanguagesReturned  = returned,
            FallbackUsed       = fallback,
            TotalMatches       = totalMatches,
            Offset             = offset,
            Limit              = limit,
            Mounts             = entries,
            GameVersion        = gameData.GameVersion,
            Timestamp          = DateTimeOffset.UtcNow.ToString("O"),
        };
    }

    private MinionsResponse BuildMinionsResponse(string? query, int limit, int offset, string[] langs)
    {
        var (returned, fallback) = gameData.Languages.ApplyFallback(langs);
        var primaryLang = returned.Contains("en") ? "en" : returned[0];

        var allMatches = new List<(uint RowId, string Name, uint Icon)>();

        foreach (var row in GetSheet<Companion>(primaryLang))
        {
            var name = row.Singular.ToString();
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (query is not null && !name.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;

            allMatches.Add((row.RowId, name, (uint)row.Icon));
        }

        var totalMatches = allMatches.Count;
        var page         = allMatches.Skip(offset).Take(limit).ToList();

        var pageRowIds = new HashSet<uint>(page.Select(x => x.RowId));
        var secondaryNames = returned
            .Where(l => l != primaryLang)
            .ToDictionary(
                lang => lang,
                lang =>
                {
                    var idx = new Dictionary<uint, string>();
                    foreach (var row in GetSheet<Companion>(lang))
                    {
                        if (!pageRowIds.Contains(row.RowId)) continue;
                        var n = row.Singular.ToString();
                        if (!string.IsNullOrWhiteSpace(n)) idx[row.RowId] = n;
                    }
                    return idx;
                });

        var entries = page.Select(m =>
        {
            var nameMap = new Dictionary<string, string> { [primaryLang] = m.Name };
            foreach (var (lang, idx) in secondaryNames)
                if (idx.TryGetValue(m.RowId, out var n)) nameMap[lang] = n;

            return new MinionEntry(m.RowId, nameMap, m.Icon);
        }).ToArray();

        return new MinionsResponse
        {
            Query              = query,
            LanguagesRequested = langs,
            LanguagesReturned  = returned,
            FallbackUsed       = fallback,
            TotalMatches       = totalMatches,
            Offset             = offset,
            Limit              = limit,
            Minions            = entries,
            GameVersion        = gameData.GameVersion,
            Timestamp          = DateTimeOffset.UtcNow.ToString("O"),
        };
    }

    private AchievementsResponse BuildAchievementsResponse(string? query, int limit, int offset, string[] langs)
    {
        var (returned, fallback) = gameData.Languages.ApplyFallback(langs);
        var primaryLang = returned.Contains("en") ? "en" : returned[0];

        // Build AchievementCategory name lookup (small sheet).
        var categoryNames = new Dictionary<uint, string>();
        foreach (var cat in GetSheet<AchievementCategory>(primaryLang))
        {
            var n = cat.Name.ToString();
            if (!string.IsNullOrWhiteSpace(n)) categoryNames[cat.RowId] = n;
        }

        // Pass 1: scan primary language, apply filters, collect fields.
        var allMatches = new List<(uint RowId, string Name, string Desc, uint Icon,
                                   uint Points, uint CategoryId)>();

        foreach (var row in GetSheet<Lumina.Excel.Sheets.Achievement>(primaryLang))
        {
            var name = row.Name.ToString();
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (query is not null && !name.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;

            allMatches.Add((
                row.RowId,
                name,
                row.Description.ToString(),
                row.Icon,
                row.Points,
                row.AchievementCategory.RowId));
        }

        var totalMatches = allMatches.Count;
        var page         = allMatches.Skip(offset).Take(limit).ToList();

        // Pass 2: secondary language strings for page rows only.
        var pageRowIds = new HashSet<uint>(page.Select(x => x.RowId));
        var secondaryStrings = returned
            .Where(l => l != primaryLang)
            .ToDictionary(
                lang => lang,
                lang =>
                {
                    var idx = new Dictionary<uint, (string Name, string Desc)>();
                    foreach (var row in GetSheet<Lumina.Excel.Sheets.Achievement>(lang))
                    {
                        if (!pageRowIds.Contains(row.RowId)) continue;
                        var n = row.Name.ToString();
                        if (!string.IsNullOrWhiteSpace(n))
                            idx[row.RowId] = (n, row.Description.ToString());
                    }
                    return idx;
                });

        var entries = page.Select(m =>
        {
            var nameMap = new Dictionary<string, string> { [primaryLang] = m.Name };
            var descMap = new Dictionary<string, string> { [primaryLang] = m.Desc };
            foreach (var (lang, idx) in secondaryStrings)
            {
                if (idx.TryGetValue(m.RowId, out var s))
                {
                    nameMap[lang] = s.Name;
                    descMap[lang] = s.Desc;
                }
            }
            return new AchievementEntry(
                RowId:                   m.RowId,
                Name:                    nameMap,
                Description:             descMap,
                Icon:                    m.Icon,
                Points:                  m.Points,
                AchievementCategoryId:   m.CategoryId,
                AchievementCategoryName: categoryNames.GetValueOrDefault(m.CategoryId, string.Empty));
        }).ToArray();

        return new AchievementsResponse
        {
            Query              = query,
            LanguagesRequested = langs,
            LanguagesReturned  = returned,
            FallbackUsed       = fallback,
            TotalMatches       = totalMatches,
            Offset             = offset,
            Limit              = limit,
            Achievements       = entries,
            GameVersion        = gameData.GameVersion,
            Timestamp          = DateTimeOffset.UtcNow.ToString("O"),
        };
    }

    private TraitsResponse BuildTraitsResponse(string? query, uint? classJobId, int limit, int offset, string[] langs)
    {
        var (returned, fallback) = gameData.Languages.ApplyFallback(langs);
        var primaryLang = returned.Contains("en") ? "en" : returned[0];

        // Pass 1: scan primary language, apply filters, collect fields.
        var allMatches = new List<(uint RowId, string Name, int Icon, uint ClassJobId, byte Level)>();

        foreach (var row in GetSheet<Trait>(primaryLang))
        {
            var name = row.Name.ToString();
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (classJobId.HasValue && row.ClassJob.RowId != classJobId.Value) continue;
            if (query is not null && !name.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;

            allMatches.Add((row.RowId, name, row.Icon, row.ClassJob.RowId, row.Level));
        }

        allMatches.Sort((a, b) =>
        {
            var lvl = a.Level.CompareTo(b.Level);
            return lvl != 0 ? lvl : a.RowId.CompareTo(b.RowId);
        });

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
                    foreach (var row in GetSheet<Trait>(lang))
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

            return new TraitEntry(
                RowId:      m.RowId,
                Name:       nameMap,
                Icon:       (uint)m.Icon,
                ClassJobId: m.ClassJobId,
                Level:      m.Level);
        }).ToArray();

        return new TraitsResponse
        {
            Query              = query,
            LanguagesRequested = langs,
            LanguagesReturned  = returned,
            FallbackUsed       = fallback,
            TotalMatches       = totalMatches,
            Offset             = offset,
            Limit              = limit,
            Traits             = entries,
            GameVersion        = gameData.GameVersion,
            Timestamp          = DateTimeOffset.UtcNow.ToString("O"),
        };
    }

    private StatusesResponse BuildStatusesResponse(string? query, string? category, int limit, int offset, string[] langs)
    {
        var (returned, fallback) = gameData.Languages.ApplyFallback(langs);
        var primaryLang = returned.Contains("en") ? "en" : returned[0];

        byte? categoryFilter = category?.ToLowerInvariant() switch
        {
            StatusCategories.BeneficialLabel  => StatusCategories.Beneficial,
            StatusCategories.DetrimentalLabel => StatusCategories.Detrimental,
            _ => null,
        };

        // Pass 1: scan primary language, apply filters, collect all fields.
        var allMatches = new List<(uint RowId, string Name, string Desc, uint Icon,
                                   byte StatusCategory,
                                   bool CanDispel, bool IsFcBuff, bool IsGaze, bool IsPermanent)>();

        foreach (var row in GetSheet<Status>(primaryLang))
        {
            var name = row.Name.ToString();
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (categoryFilter.HasValue && row.StatusCategory != categoryFilter.Value) continue;
            if (query is not null && !name.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;

            allMatches.Add((
                row.RowId,
                name,
                row.Description.ToString(),
                row.Icon,
                row.StatusCategory,
                row.CanDispel,
                row.IsFcBuff,
                row.IsGaze,
                row.IsPermanent));
        }

        var totalMatches = allMatches.Count;
        var page         = allMatches.Skip(offset).Take(limit).ToList();

        // Pass 2: secondary language strings for page rows only.
        var pageRowIds = new HashSet<uint>(page.Select(x => x.RowId));
        var secondaryStrings = returned
            .Where(l => l != primaryLang)
            .ToDictionary(
                lang => lang,
                lang =>
                {
                    var idx = new Dictionary<uint, (string Name, string Desc)>();
                    foreach (var row in GetSheet<Status>(lang))
                    {
                        if (!pageRowIds.Contains(row.RowId)) continue;
                        var n = row.Name.ToString();
                        if (!string.IsNullOrWhiteSpace(n))
                            idx[row.RowId] = (n, row.Description.ToString());
                    }
                    return idx;
                });

        var entries = page.Select(m =>
        {
            var nameMap = new Dictionary<string, string> { [primaryLang] = m.Name };
            var descMap = new Dictionary<string, string> { [primaryLang] = m.Desc };
            foreach (var (lang, idx) in secondaryStrings)
            {
                if (idx.TryGetValue(m.RowId, out var s))
                {
                    nameMap[lang] = s.Name;
                    descMap[lang] = s.Desc;
                }
            }
            return new StatusEntry(
                RowId:              m.RowId,
                Name:               nameMap,
                Description:        descMap,
                Icon:               m.Icon,
                StatusCategory:     m.StatusCategory,
                StatusCategoryName: StatusCategories.Resolve(m.StatusCategory),
                CanDispel:          m.CanDispel,
                IsFcBuff:           m.IsFcBuff,
                IsGaze:             m.IsGaze,
                IsPermanent:        m.IsPermanent);
        }).ToArray();

        return new StatusesResponse
        {
            Query              = query,
            Category           = category,
            LanguagesRequested = langs,
            LanguagesReturned  = returned,
            FallbackUsed       = fallback,
            TotalMatches       = totalMatches,
            Offset             = offset,
            Limit              = limit,
            Statuses           = entries,
            GameVersion        = gameData.GameVersion,
            Timestamp          = DateTimeOffset.UtcNow.ToString("O"),
        };
    }

    // ── New builders ─────────────────────────────────────────────────────

    private RacesResponse BuildRacesResponse(string[] langs)
    {
        var (returned, fallback) = gameData.Languages.ApplyFallback(langs);

        // Small sheet — load all languages at once (same pattern as BuildJobsResponse).
        var langData = returned.ToDictionary(
            lang => lang,
            lang =>
            {
                var idx = new Dictionary<uint, (string Masc, string Fem)>();
                foreach (var row in GetSheet<Race>(lang))
                {
                    if (row.RowId == 0) continue;
                    var m = row.Masculine.ToString();
                    if (!string.IsNullOrWhiteSpace(m))
                        idx[row.RowId] = (m, row.Feminine.ToString());
                }
                return idx;
            });

        var primaryLang = returned.Contains("en") ? "en" : returned[0];
        var entries     = new List<RaceEntry>();

        foreach (var row in GetSheet<Race>(primaryLang))
        {
            if (row.RowId == 0) continue;
            if (!langData[primaryLang].ContainsKey(row.RowId)) continue;

            var mascMap = new Dictionary<string, string>();
            var femMap  = new Dictionary<string, string>();
            foreach (var (lang, idx) in langData)
            {
                if (idx.TryGetValue(row.RowId, out var data))
                {
                    mascMap[lang] = data.Masc;
                    femMap[lang]  = data.Fem;
                }
            }
            entries.Add(new RaceEntry(row.RowId, mascMap, femMap));
        }

        return new RacesResponse
        {
            LanguagesRequested = langs,
            LanguagesReturned  = returned,
            FallbackUsed       = fallback,
            Races              = entries.ToArray(),
            GameVersion        = gameData.GameVersion,
            Timestamp          = DateTimeOffset.UtcNow.ToString("O"),
        };
    }

    private WorldsResponse BuildWorldsResponse(string? query)
    {
        // World names are proper nouns — same in all languages; use "en" only.
        // Pre-index DataCenter names from WorldDCGroupType.
        var dcNames = new Dictionary<uint, string>();
        foreach (var dc in GetSheet<WorldDCGroupType>("en"))
        {
            var n = dc.Name.ToString();
            if (!string.IsNullOrWhiteSpace(n)) dcNames[dc.RowId] = n;
        }

        var allMatches = new List<(uint RowId, string Name, string InternalName,
                                   uint DataCenterId, bool IsPublic)>();

        foreach (var row in GetSheet<World>("en"))
        {
            if (!row.IsPublic) continue;
            var name = row.Name.ToString();
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (query is not null && !name.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;

            allMatches.Add((row.RowId, name, row.InternalName.ToString(),
                            row.DataCenter.RowId, row.IsPublic));
        }

        var entries = allMatches
            .Select(m => new WorldEntry(
                RowId:          m.RowId,
                Name:           m.Name,
                InternalName:   m.InternalName,
                DataCenterId:   m.DataCenterId,
                DataCenterName: dcNames.GetValueOrDefault(m.DataCenterId, string.Empty),
                IsPublic:       m.IsPublic))
            .ToArray();

        return new WorldsResponse
        {
            Query        = query,
            TotalMatches = entries.Length,
            Worlds       = entries,
            GameVersion  = gameData.GameVersion,
            Timestamp    = DateTimeOffset.UtcNow.ToString("O"),
        };
    }

    private WeatherResponse BuildWeatherResponse(string? query, int limit, int offset, string[] langs)
    {
        var (returned, fallback) = gameData.Languages.ApplyFallback(langs);
        var primaryLang = returned.Contains("en") ? "en" : returned[0];

        var allMatches = new List<(uint RowId, string Name, uint Icon)>();

        foreach (var row in GetSheet<Weather>(primaryLang))
        {
            var name = row.Name.ToString();
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (query is not null && !name.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;

            allMatches.Add((row.RowId, name, (uint)row.Icon));
        }

        var totalMatches = allMatches.Count;
        var page         = allMatches.Skip(offset).Take(limit).ToList();

        var pageRowIds = new HashSet<uint>(page.Select(x => x.RowId));
        var secondaryNames = returned
            .Where(l => l != primaryLang)
            .ToDictionary(
                lang => lang,
                lang =>
                {
                    var idx = new Dictionary<uint, string>();
                    foreach (var row in GetSheet<Weather>(lang))
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

            return new WeatherEntry(m.RowId, nameMap, m.Icon);
        }).ToArray();

        return new WeatherResponse
        {
            Query              = query,
            LanguagesRequested = langs,
            LanguagesReturned  = returned,
            FallbackUsed       = fallback,
            TotalMatches       = totalMatches,
            Offset             = offset,
            Limit              = limit,
            Weathers           = entries,
            GameVersion        = gameData.GameVersion,
            Timestamp          = DateTimeOffset.UtcNow.ToString("O"),
        };
    }

    private TitlesResponse BuildTitlesResponse(string? query, int limit, int offset, string[] langs)
    {
        var (returned, fallback) = gameData.Languages.ApplyFallback(langs);
        var primaryLang = returned.Contains("en") ? "en" : returned[0];

        var allMatches = new List<(uint RowId, string Masc, string Fem, bool IsPrefix)>();

        foreach (var row in GetSheet<Title>(primaryLang))
        {
            var masc = row.Masculine.ToString();
            var fem  = row.Feminine.ToString();
            // Skip empty rows (both masculine and feminine empty).
            if (string.IsNullOrWhiteSpace(masc) && string.IsNullOrWhiteSpace(fem)) continue;
            // Query matches either form.
            if (query is not null
                && !masc.Contains(query, StringComparison.OrdinalIgnoreCase)
                && !fem.Contains(query,  StringComparison.OrdinalIgnoreCase)) continue;

            allMatches.Add((row.RowId, masc, fem, row.IsPrefix));
        }

        var totalMatches = allMatches.Count;
        var page         = allMatches.Skip(offset).Take(limit).ToList();

        var pageRowIds = new HashSet<uint>(page.Select(x => x.RowId));
        var secondaryStrings = returned
            .Where(l => l != primaryLang)
            .ToDictionary(
                lang => lang,
                lang =>
                {
                    var idx = new Dictionary<uint, (string Masc, string Fem)>();
                    foreach (var row in GetSheet<Title>(lang))
                    {
                        if (!pageRowIds.Contains(row.RowId)) continue;
                        idx[row.RowId] = (row.Masculine.ToString(), row.Feminine.ToString());
                    }
                    return idx;
                });

        var entries = page.Select(m =>
        {
            var mascMap = new Dictionary<string, string> { [primaryLang] = m.Masc };
            var femMap  = new Dictionary<string, string> { [primaryLang] = m.Fem };
            foreach (var (lang, idx) in secondaryStrings)
            {
                if (idx.TryGetValue(m.RowId, out var s))
                {
                    mascMap[lang] = s.Masc;
                    femMap[lang]  = s.Fem;
                }
            }
            return new TitleEntry(m.RowId, mascMap, femMap, m.IsPrefix);
        }).ToArray();

        return new TitlesResponse
        {
            Query              = query,
            LanguagesRequested = langs,
            LanguagesReturned  = returned,
            FallbackUsed       = fallback,
            TotalMatches       = totalMatches,
            Offset             = offset,
            Limit              = limit,
            Titles             = entries,
            GameVersion        = gameData.GameVersion,
            Timestamp          = DateTimeOffset.UtcNow.ToString("O"),
        };
    }

    private CurrenciesResponse BuildCurrenciesResponse(string[] langs)
    {
        var (returned, fallback) = gameData.Languages.ApplyFallback(langs);
        var primaryLang = returned.Contains("en") ? "en" : returned[0];

        // Currencies: ItemUICategory=63, FilterGroup=16, StackSize>1.
        // UIcat=63 alone is too broad (includes bardings, floatstones, etc.).
        // FilterGroup=16 removes non-currency "Other" items. StackSize>1 removes
        // single-use/equipment items that share the same category (e.g. bardings, StackSize=1).
        var allMatches = new List<(uint RowId, string Name, uint Icon, uint StackSize)>();

        foreach (var row in GetSheet<Item>(primaryLang))
        {
            if (row.ItemUICategory.RowId != 63) continue;
            if (row.FilterGroup != 16) continue;
            if (row.StackSize <= 1) continue;
            var name = row.Name.ToString();
            if (string.IsNullOrWhiteSpace(name)) continue;

            allMatches.Add((row.RowId, name, row.Icon, row.StackSize));
        }

        // Collect secondary language names for all matched rows.
        var matchedRowIds = new HashSet<uint>(allMatches.Select(x => x.RowId));
        var secondaryNames = returned
            .Where(l => l != primaryLang)
            .ToDictionary(
                lang => lang,
                lang =>
                {
                    var idx = new Dictionary<uint, string>();
                    foreach (var row in GetSheet<Item>(lang))
                    {
                        if (!matchedRowIds.Contains(row.RowId)) continue;
                        var n = row.Name.ToString();
                        if (!string.IsNullOrWhiteSpace(n)) idx[row.RowId] = n;
                    }
                    return idx;
                });

        var entries = allMatches.Select(m =>
        {
            var nameMap = new Dictionary<string, string> { [primaryLang] = m.Name };
            foreach (var (lang, idx) in secondaryNames)
                if (idx.TryGetValue(m.RowId, out var n)) nameMap[lang] = n;

            return new CurrencyEntry(m.RowId, nameMap, m.Icon, m.StackSize);
        }).ToArray();

        return new CurrenciesResponse
        {
            LanguagesRequested = langs,
            LanguagesReturned  = returned,
            FallbackUsed       = fallback,
            Currencies         = entries,
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
