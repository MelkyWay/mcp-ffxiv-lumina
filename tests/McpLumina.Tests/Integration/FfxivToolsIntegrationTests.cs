using System.Text.Json;
using McpLumina.Constants;
using McpLumina.Models.Responses;
using McpLumina.Tools;
using VerifyXunit;
using Xunit;

namespace McpLumina.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class FfxivToolsIntegrationTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    // ── get_jobs ───────────────────────────────────────────────────────────

    [SkippableFact]
    public void GetJobs_English_ContainsExpectedJobs()
    {
        SkipIfNoGamePath();

        var response = BuildJobsResponse(["en"]);

        var paladin = response.Jobs.FirstOrDefault(j =>
            string.Equals(j.Name.GetValueOrDefault("en"), "paladin", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(paladin);
        Assert.Equal("Tank", paladin.Role);
        Assert.True(paladin.IsJob);
        Assert.False(paladin.IsLimited);

        var blueMage = response.Jobs.FirstOrDefault(j =>
            j.Name.GetValueOrDefault("en")?.Contains("blue mage", StringComparison.OrdinalIgnoreCase) == true);
        Assert.NotNull(blueMage);
        Assert.True(blueMage.IsLimited);
        Assert.Equal(RoleLabels.Limited, blueMage.Role);
    }

    [SkippableFact]
    public void GetJobs_MultiLanguage_ReturnsBothLanguages()
    {
        SkipIfNoGamePath();

        var response = BuildJobsResponse(["en", "ja"]);

        var warrior = response.Jobs.FirstOrDefault(j =>
            string.Equals(j.Name.GetValueOrDefault("en"), "warrior", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(warrior);
        Assert.True(warrior.Name.ContainsKey("ja"), "Warrior should have Japanese name.");
        Assert.False(string.IsNullOrWhiteSpace(warrior.Name["ja"]));
    }

    [SkippableFact]
    public async Task GetJobs_Snapshot_MatchesExpected()
    {
        SkipIfNoGamePath();

        var response = BuildJobsResponse(["en"]);
        var snapshot = new
        {
            response.LanguagesRequested,
            response.LanguagesReturned,
            response.FallbackUsed,
            response.Jobs,
            response.GameVersion,
        };
        var json = JsonSerializer.Serialize(snapshot, JsonOpts);

        await Verifier.Verify(json, "json")
            .UseDirectory("Snapshots")
            .UseFileName("get_jobs_en");
    }

    // ── get_duties ─────────────────────────────────────────────────────────

    [SkippableFact]
    public void GetDuties_All_ReturnsNonEmpty()
    {
        SkipIfNoGamePath();

        var response = BuildDutiesResponse(null, ["en"]);
        Assert.True(response.Count > 0, "Expected at least one duty.");
        Assert.NotEmpty(response.Duties);
    }

    [SkippableFact]
    public void GetDuties_Dungeon_FilteredCorrectly()
    {
        SkipIfNoGamePath();

        var response = BuildDutiesResponse("dungeon", ["en"]);
        Assert.All(response.Duties, d => Assert.Equal("dungeon", d.Category));
    }

    [SkippableFact]
    public void GetDuties_Ultimate_ContainsKnownDuty()
    {
        SkipIfNoGamePath();

        var response = BuildDutiesResponse("ultimate", ["en"]);
        Assert.NotEmpty(response.Duties);

        var weaponsRefrain = response.Duties.FirstOrDefault(d =>
            d.Name.GetValueOrDefault("en")?.Contains("Weapon") == true);
        Assert.NotNull(weaponsRefrain);
    }

    [SkippableFact]
    public async Task GetDuties_Snapshot_MatchesExpected()
    {
        SkipIfNoGamePath();

        var response = BuildDutiesResponse("ultimate", ["en"]);
        var snapshot = new
        {
            response.Category,
            response.LanguagesRequested,
            response.LanguagesReturned,
            response.FallbackUsed,
            response.Count,
            response.Duties,
            response.GameVersion,
        };
        var json = JsonSerializer.Serialize(snapshot, JsonOpts);

        await Verifier.Verify(json, "json")
            .UseDirectory("Snapshots")
            .UseFileName("get_duties_ultimates_en");
    }

    [SkippableFact]
    public void GetDuties_UnrealMixedCase_FiltersToUnrealOnly()
    {
        SkipIfNoGamePath();

        // Validator accepts "UNREAL" (normalises for the contains check) but the filter
        // inside BuildDutiesResponse previously used exact equality — "UNREAL" != "unreal"
        // — so the suffix guard was skipped and all trials were returned instead.
        var response = BuildDutiesResponse("UNREAL", ["en"]);

        Assert.NotEmpty(response.Duties);
        Assert.All(response.Duties, d =>
        {
            var name = d.Name.GetValueOrDefault("en") ?? string.Empty;
            Assert.Contains("(Unreal)", name, StringComparison.OrdinalIgnoreCase);
        });
    }

    // ── get_actions ───────────────────────────────────────────────────────

    [SkippableFact]
    public void GetActions_NoFilter_ReturnsPlayerActionsOnly()
    {
        SkipIfNoGamePath();

        var response = BuildActionsResponse(null, null);

        Assert.True(response.TotalMatches > 0);
        // All returned actions must have a non-empty name
        Assert.All(response.Actions, a => Assert.False(string.IsNullOrWhiteSpace(a.Name.GetValueOrDefault("en"))));
        // Sanity: player action sheet has hundreds of real actions
        Assert.True(response.TotalMatches > 100, $"Expected >100 player actions, got {response.TotalMatches}");
    }

    [SkippableFact]
    public void GetActions_QueryFire_ContainsFireSpell()
    {
        SkipIfNoGamePath();

        var response = BuildActionsResponse("Fire", null);

        Assert.NotEmpty(response.Actions);
        var fire = response.Actions.FirstOrDefault(a =>
            a.Name.GetValueOrDefault("en") == "Fire");
        Assert.NotNull(fire);
        Assert.Equal(7u, fire.ClassJobId);       // Thaumaturge
        Assert.Equal(2u, fire.ActionCategoryId); // Spell
        Assert.Equal(2000u, fire.CastTimeMs);
    }

    [SkippableFact]
    public void GetActions_ClassJobFilter_ReturnsOnlyThatJob()
    {
        SkipIfNoGamePath();

        // Black Mage = row ID 25
        var response = BuildActionsResponse(null, 25);

        Assert.NotEmpty(response.Actions);
        Assert.All(response.Actions, a =>
            Assert.Equal(25u, a.ClassJobId));
    }

    [SkippableFact]
    public void GetActions_QueryAndClassJobFilter_Intersects()
    {
        SkipIfNoGamePath();

        var response = BuildActionsResponse("Blizzard", 25);

        Assert.NotEmpty(response.Actions);
        Assert.All(response.Actions, a =>
        {
            Assert.Equal(25u, a.ClassJobId);
            Assert.Contains("Blizzard", a.Name.GetValueOrDefault("en") ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);
        });
    }

    [SkippableFact]
    public void GetActions_MultiLanguage_ReturnsBothLanguages()
    {
        SkipIfNoGamePath();

        var response = BuildActionsResponse("Fire", null, ["en", "ja"]);

        var fire = response.Actions.FirstOrDefault(a => a.Name.GetValueOrDefault("en") == "Fire");
        Assert.NotNull(fire);
        Assert.True(fire.Name.ContainsKey("ja"), "Fire should have a Japanese name.");
        Assert.False(string.IsNullOrWhiteSpace(fire.Name["ja"]));
    }

    [SkippableFact]
    public void GetActions_Pagination_OffsetAdvancesResults()
    {
        SkipIfNoGamePath();

        var page0 = BuildActionsResponse(null, null, ["en"], limit: 10, offset: 0);
        var page1 = BuildActionsResponse(null, null, ["en"], limit: 10, offset: 10);

        Assert.Equal(10, page0.Actions.Length);
        Assert.Equal(10, page1.Actions.Length);
        Assert.Empty(page0.Actions.Select(a => a.RowId)
            .Intersect(page1.Actions.Select(a => a.RowId)));
    }

    [SkippableFact]
    public void GetActions_NegativeClassJobId_Throws()
    {
        SkipIfNoGamePath();

        var json = Tools.GetActions(classJobId: -1);
        Assert.Contains("ValidationError", json);
    }

    // ── get_mounts ────────────────────────────────────────────────────────

    [SkippableFact]
    public void GetMounts_NoFilter_ReturnsNonEmpty()
    {
        SkipIfNoGamePath();

        var response = BuildMountsResponse(null);

        Assert.True(response.TotalMatches > 0);
        Assert.All(response.Mounts, m => Assert.False(string.IsNullOrWhiteSpace(m.Name.GetValueOrDefault("en"))));
        Assert.True(response.TotalMatches > 100, $"Expected >100 mounts, got {response.TotalMatches}");
    }

    [SkippableFact]
    public void GetMounts_QueryChocobo_ContainsChocobo()
    {
        SkipIfNoGamePath();

        var response = BuildMountsResponse("Chocobo");

        Assert.NotEmpty(response.Mounts);
        Assert.All(response.Mounts, m =>
            Assert.Contains("Chocobo", m.Name.GetValueOrDefault("en") ?? string.Empty,
                StringComparison.OrdinalIgnoreCase));
    }

    [SkippableFact]
    public void GetMounts_FlyingMountsExist()
    {
        SkipIfNoGamePath();

        var response = BuildMountsResponse(null, limit: 200);

        Assert.Contains(response.Mounts, m => m.IsFlying);
    }

    [SkippableFact]
    public void GetMounts_MultiSeatMountsExist()
    {
        SkipIfNoGamePath();

        var response = BuildMountsResponse(null, limit: 200);

        Assert.Contains(response.Mounts, m => m.ExtraSeats > 0);
    }

    [SkippableFact]
    public void GetMounts_MultiLanguage_ReturnsBothLanguages()
    {
        SkipIfNoGamePath();

        var response = BuildMountsResponse("Chocobo", langs: ["en", "ja"]);

        var mount = response.Mounts.First();
        Assert.True(mount.Name.ContainsKey("ja"), "Mount should have a Japanese name.");
        Assert.False(string.IsNullOrWhiteSpace(mount.Name["ja"]));
    }

    // ── get_minions ───────────────────────────────────────────────────────

    [SkippableFact]
    public void GetMinions_NoFilter_ReturnsNonEmpty()
    {
        SkipIfNoGamePath();

        var response = BuildMinionsResponse(null);

        Assert.True(response.TotalMatches > 0);
        Assert.All(response.Minions, m => Assert.False(string.IsNullOrWhiteSpace(m.Name.GetValueOrDefault("en"))));
        Assert.True(response.TotalMatches > 100, $"Expected >100 minions, got {response.TotalMatches}");
    }

    [SkippableFact]
    public void GetMinions_QueryBahamut_ContainsBahamut()
    {
        SkipIfNoGamePath();

        var response = BuildMinionsResponse("Bahamut");

        Assert.NotEmpty(response.Minions);
        Assert.All(response.Minions, m =>
            Assert.Contains("Bahamut", m.Name.GetValueOrDefault("en") ?? string.Empty,
                StringComparison.OrdinalIgnoreCase));
    }

    [SkippableFact]
    public void GetMinions_MultiLanguage_ReturnsBothLanguages()
    {
        SkipIfNoGamePath();

        var response = BuildMinionsResponse("Bahamut", langs: ["en", "ja"]);

        var minion = response.Minions.First();
        Assert.True(minion.Name.ContainsKey("ja"), "Minion should have a Japanese name.");
        Assert.False(string.IsNullOrWhiteSpace(minion.Name["ja"]));
    }

    [SkippableFact]
    public void GetMinions_AllHaveIcons()
    {
        SkipIfNoGamePath();

        var response = BuildMinionsResponse(null, limit: 50);

        Assert.All(response.Minions, m => Assert.True(m.Icon > 0, $"Minion '{m.Name.GetValueOrDefault("en")}' has no icon."));
    }

    // ── get_achievements ──────────────────────────────────────────────────

    [SkippableFact]
    public void GetAchievements_NoFilter_ReturnsNonEmpty()
    {
        SkipIfNoGamePath();

        var response = BuildAchievementsResponse(null);

        Assert.True(response.TotalMatches > 0);
        Assert.All(response.Achievements, a => Assert.False(string.IsNullOrWhiteSpace(a.Name.GetValueOrDefault("en"))));
        Assert.True(response.TotalMatches > 500, $"Expected >500 achievements, got {response.TotalMatches}");
    }

    [SkippableFact]
    public void GetAchievements_Query_FiltersCorrectly()
    {
        SkipIfNoGamePath();

        var response = BuildAchievementsResponse("Shadowbringers");

        Assert.NotEmpty(response.Achievements);
        Assert.All(response.Achievements, a =>
            Assert.Contains("Shadowbringers", a.Name.GetValueOrDefault("en") ?? string.Empty,
                StringComparison.OrdinalIgnoreCase));
    }

    [SkippableFact]
    public void GetAchievements_KnownAchievement_HasExpectedFields()
    {
        SkipIfNoGamePath();

        var response = BuildAchievementsResponse("To Crush Your Enemies I");

        var achievement = response.Achievements.FirstOrDefault(a =>
            a.Name.GetValueOrDefault("en") == "To Crush Your Enemies I");
        Assert.NotNull(achievement);
        Assert.True(achievement.Points > 0, "Points should be > 0.");
        Assert.True(achievement.Icon > 0, "Icon should be > 0.");
        Assert.False(string.IsNullOrWhiteSpace(achievement.AchievementCategoryName));
        Assert.False(string.IsNullOrWhiteSpace(achievement.Description.GetValueOrDefault("en")));
    }

    [SkippableFact]
    public void GetAchievements_MultiLanguage_ReturnsBothLanguages()
    {
        SkipIfNoGamePath();

        var response = BuildAchievementsResponse("To Crush Your Enemies I", langs: ["en", "ja"]);

        var achievement = response.Achievements.FirstOrDefault(a =>
            a.Name.GetValueOrDefault("en") == "To Crush Your Enemies I");
        Assert.NotNull(achievement);
        Assert.True(achievement.Name.ContainsKey("ja"), "Achievement should have a Japanese name.");
        Assert.False(string.IsNullOrWhiteSpace(achievement.Name["ja"]));
    }

    [SkippableFact]
    public void GetAchievements_Pagination_OffsetAdvancesResults()
    {
        SkipIfNoGamePath();

        var page0 = BuildAchievementsResponse(null, limit: 10, offset: 0);
        var page1 = BuildAchievementsResponse(null, limit: 10, offset: 10);

        Assert.Equal(10, page0.Achievements.Length);
        Assert.Equal(10, page1.Achievements.Length);
        Assert.Empty(page0.Achievements.Select(a => a.RowId)
            .Intersect(page1.Achievements.Select(a => a.RowId)));
    }

    // ── get_traits ────────────────────────────────────────────────────────

    [SkippableFact]
    public void GetTraits_NoFilter_ReturnsNonEmpty()
    {
        SkipIfNoGamePath();

        var response = BuildTraitsResponse(null, null);

        Assert.True(response.TotalMatches > 0);
        Assert.All(response.Traits, t => Assert.False(string.IsNullOrWhiteSpace(t.Name.GetValueOrDefault("en"))));
        Assert.True(response.TotalMatches > 100, $"Expected >100 traits, got {response.TotalMatches}");
    }

    [SkippableFact]
    public void GetTraits_ClassJobFilter_ReturnsOnlyThatJob()
    {
        SkipIfNoGamePath();

        // White Mage = row ID 24
        var response = BuildTraitsResponse(null, 24);

        Assert.NotEmpty(response.Traits);
        Assert.All(response.Traits, t => Assert.Equal(24u, t.ClassJobId));
    }

    [SkippableFact]
    public void GetTraits_ClassJobFilter_OrderedByLevel()
    {
        SkipIfNoGamePath();

        var response = BuildTraitsResponse(null, 24, limit: 50);

        var levels = response.Traits.Select(t => t.Level).ToList();
        Assert.Equal(levels.OrderBy(l => l).ToList(), levels);
    }

    [SkippableFact]
    public void GetTraits_QueryEnhancedHealingMagic_ContainsWhm()
    {
        SkipIfNoGamePath();

        var response = BuildTraitsResponse("Enhanced Healing Magic", null);

        Assert.NotEmpty(response.Traits);
        var whmTrait = response.Traits.FirstOrDefault(t => t.ClassJobId == 24u);
        Assert.NotNull(whmTrait);
        Assert.Equal(85u, whmTrait.Level);
    }

    [SkippableFact]
    public void GetTraits_MultiLanguage_ReturnsBothLanguages()
    {
        SkipIfNoGamePath();

        var response = BuildTraitsResponse("Enhanced Healing Magic", 24, ["en", "ja"]);

        Assert.NotEmpty(response.Traits);
        var trait = response.Traits.First();
        Assert.True(trait.Name.ContainsKey("ja"), "Trait should have a Japanese name.");
        Assert.False(string.IsNullOrWhiteSpace(trait.Name["ja"]));
    }

    [SkippableFact]
    public void GetTraits_NegativeClassJobId_Throws()
    {
        SkipIfNoGamePath();

        var json = Tools.GetTraits(classJobId: -1);
        Assert.Contains("ValidationError", json);
    }

    // ── get_statuses ──────────────────────────────────────────────────────

    [SkippableFact]
    public void GetStatuses_NoFilter_ReturnsNamedStatuses()
    {
        SkipIfNoGamePath();

        var response = BuildStatusesResponse(null, null);

        Assert.True(response.TotalMatches > 0);
        Assert.All(response.Statuses, s => Assert.False(string.IsNullOrWhiteSpace(s.Name.GetValueOrDefault("en"))));
        Assert.True(response.TotalMatches > 500, $"Expected >500 statuses, got {response.TotalMatches}");
    }

    [SkippableFact]
    public void GetStatuses_QueryParalysis_IsDetrimental()
    {
        SkipIfNoGamePath();

        var response = BuildStatusesResponse("Paralysis", null);

        Assert.NotEmpty(response.Statuses);
        var paralysis = response.Statuses.First();
        Assert.Equal("detrimental", paralysis.StatusCategoryName);
        Assert.Equal(2, paralysis.StatusCategory);
        Assert.True(paralysis.CanDispel);
    }

    [SkippableFact]
    public void GetStatuses_QueryMediated_IsBeneficial()
    {
        SkipIfNoGamePath();

        var response = BuildStatusesResponse("Medicated", "beneficial");

        Assert.NotEmpty(response.Statuses);
        var medicated = response.Statuses.FirstOrDefault(s =>
            s.Name.GetValueOrDefault("en")?.Equals("Medicated", StringComparison.OrdinalIgnoreCase) == true);
        Assert.NotNull(medicated);
        Assert.Equal("beneficial", medicated.StatusCategoryName);
        Assert.Equal(1, medicated.StatusCategory);
    }

    [SkippableFact]
    public void GetStatuses_CategoryBeneficial_AllAreBeneficial()
    {
        SkipIfNoGamePath();

        var response = BuildStatusesResponse(null, "beneficial", limit: 50);

        Assert.NotEmpty(response.Statuses);
        Assert.All(response.Statuses, s =>
        {
            Assert.Equal("beneficial", s.StatusCategoryName);
            Assert.Equal(1, s.StatusCategory);
        });
    }

    [SkippableFact]
    public void GetStatuses_CategoryDetrimental_AllAreDetrimental()
    {
        SkipIfNoGamePath();

        var response = BuildStatusesResponse(null, "detrimental", limit: 50);

        Assert.NotEmpty(response.Statuses);
        Assert.All(response.Statuses, s =>
        {
            Assert.Equal("detrimental", s.StatusCategoryName);
            Assert.Equal(2, s.StatusCategory);
        });
    }

    [SkippableFact]
    public void GetStatuses_MultiLanguage_ReturnsBothLanguages()
    {
        SkipIfNoGamePath();

        var response = BuildStatusesResponse("Paralysis", null, ["en", "ja"]);

        var paralysis = response.Statuses.First();
        Assert.True(paralysis.Name.ContainsKey("ja"), "Paralysis should have a Japanese name.");
        Assert.False(string.IsNullOrWhiteSpace(paralysis.Name["ja"]));
    }

    [SkippableFact]
    public void GetStatuses_InvalidCategory_ReturnsValidationError()
    {
        SkipIfNoGamePath();

        var json = Tools.GetStatuses(category: "nonsense");
        Assert.Contains("ValidationError", json);
    }

    // ── get_races ──────────────────────────────────────────────────────────

    [SkippableFact]
    public void GetRaces_English_ContainsAllBaseRaces()
    {
        SkipIfNoGamePath();

        var response = BuildRacesResponse(["en"]);

        Assert.NotEmpty(response.Races);
        // There are 8 playable races.
        Assert.Equal(8, response.Races.Length);
    }

    [SkippableFact]
    public void GetRaces_English_HyurHasMasculineAndFeminineNames()
    {
        SkipIfNoGamePath();

        var response = BuildRacesResponse(["en"]);

        var hyur = response.Races.FirstOrDefault(r => r.RowId == 1);
        Assert.NotNull(hyur);
        Assert.True(hyur.Masculine.ContainsKey("en"));
        Assert.True(hyur.Feminine.ContainsKey("en"));
        Assert.Equal("Hyur", hyur.Masculine["en"], ignoreCase: true);
    }

    [SkippableFact]
    public void GetRaces_MultiLanguage_ContainsJapanese()
    {
        SkipIfNoGamePath();

        var response = BuildRacesResponse(["en", "ja"]);

        var miqote = response.Races.FirstOrDefault(r => r.RowId == 4);
        Assert.NotNull(miqote);
        Assert.True(miqote.Masculine.ContainsKey("ja"), "Miqo'te should have a Japanese masculine name.");
        Assert.False(string.IsNullOrWhiteSpace(miqote.Masculine["ja"]));
    }

    // ── get_worlds ─────────────────────────────────────────────────────────

    [SkippableFact]
    public void GetWorlds_All_ReturnsPublicServers()
    {
        SkipIfNoGamePath();

        var response = BuildWorldsResponse();

        Assert.True(response.TotalMatches > 50, "Expected many public worlds.");
        Assert.All(response.Worlds, w => Assert.True(w.IsPublic));
    }

    [SkippableFact]
    public void GetWorlds_All_DataCenterNamesPopulated()
    {
        SkipIfNoGamePath();

        var response = BuildWorldsResponse();

        Assert.All(response.Worlds, w =>
            Assert.False(string.IsNullOrWhiteSpace(w.DataCenterName),
                $"World '{w.Name}' has empty DataCenterName."));
    }

    [SkippableFact]
    public void GetWorlds_QueryAether_ReturnsAetherServers()
    {
        SkipIfNoGamePath();

        // "Cactuar" is a known Aether DC server.
        var response = BuildWorldsResponse(query: "Cactuar");

        Assert.NotEmpty(response.Worlds);
        var cactuar = response.Worlds.FirstOrDefault(w =>
            w.Name.Equals("Cactuar", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(cactuar);
        Assert.Equal("Aether", cactuar.DataCenterName, ignoreCase: true);
    }

    // ── get_weather ────────────────────────────────────────────────────────

    [SkippableFact]
    public void GetWeather_All_ReturnsNonEmpty()
    {
        SkipIfNoGamePath();

        var response = BuildWeatherResponse(null, limit: 10);

        Assert.NotEmpty(response.Weathers);
        Assert.True(response.TotalMatches > 10);
    }

    [SkippableFact]
    public void GetWeather_QueryClear_ReturnsClearSkies()
    {
        SkipIfNoGamePath();

        var response = BuildWeatherResponse("clear");

        var clearSkies = response.Weathers.FirstOrDefault(w =>
            w.Name.GetValueOrDefault("en")?.Contains("clear", StringComparison.OrdinalIgnoreCase) == true);
        Assert.NotNull(clearSkies);
        Assert.True(clearSkies.Icon > 0, "Clear Skies should have a non-zero icon ID.");
    }

    [SkippableFact]
    public void GetWeather_MultiLanguage_ContainsJapanese()
    {
        SkipIfNoGamePath();

        var response = BuildWeatherResponse("rain", langs: ["en", "ja"]);

        Assert.NotEmpty(response.Weathers);
        var first = response.Weathers.First();
        Assert.True(first.Name.ContainsKey("ja"), "Rain weather should have a Japanese name.");
        Assert.False(string.IsNullOrWhiteSpace(first.Name["ja"]));
    }

    // ── get_titles ─────────────────────────────────────────────────────────

    [SkippableFact]
    public void GetTitles_All_ReturnsNonEmpty()
    {
        SkipIfNoGamePath();

        var response = BuildTitlesResponse(null, limit: 10);

        Assert.NotEmpty(response.Titles);
        Assert.True(response.TotalMatches > 100);
    }

    [SkippableFact]
    public void GetTitles_QueryInsatiable_ReturnsMatch()
    {
        SkipIfNoGamePath();

        var response = BuildTitlesResponse("Insatiable");

        Assert.NotEmpty(response.Titles);
        var title = response.Titles.First();
        Assert.True(
            title.Masculine.GetValueOrDefault("en")?.Contains("Insatiable", StringComparison.OrdinalIgnoreCase) == true ||
            title.Feminine.GetValueOrDefault("en")?.Contains("Insatiable", StringComparison.OrdinalIgnoreCase)  == true);
    }

    [SkippableFact]
    public void GetTitles_HasPrefixAndSuffixTitles()
    {
        SkipIfNoGamePath();

        var response = BuildTitlesResponse(null, limit: 200);

        Assert.True(response.Titles.Any(t =>  t.IsPrefix), "Expected at least one prefix title.");
        Assert.True(response.Titles.Any(t => !t.IsPrefix), "Expected at least one suffix title.");
    }

    [SkippableFact]
    public void GetTitles_MultiLanguage_ContainsJapanese()
    {
        SkipIfNoGamePath();

        var response = BuildTitlesResponse("Insatiable", langs: ["en", "ja"]);

        Assert.NotEmpty(response.Titles);
        var title = response.Titles.First();
        Assert.True(title.Masculine.ContainsKey("ja") || title.Feminine.ContainsKey("ja"),
            "Title should have a Japanese form.");
    }

    // ── get_currencies ─────────────────────────────────────────────────────

    [SkippableFact]
    public void GetCurrencies_ContainsGil()
    {
        SkipIfNoGamePath();

        var response = BuildCurrenciesResponse(["en"]);

        var gil = response.Currencies.FirstOrDefault(c =>
            c.Name.GetValueOrDefault("en")?.Equals("Gil", StringComparison.OrdinalIgnoreCase) == true);
        Assert.NotNull(gil);
        Assert.Equal(1u, gil.RowId);
        Assert.Equal(999999999u, gil.StackSize);
    }

    [SkippableFact]
    public void GetCurrencies_ContainsTomestone()
    {
        SkipIfNoGamePath();

        var response = BuildCurrenciesResponse(["en"]);

        var tomestone = response.Currencies.FirstOrDefault(c =>
            c.Name.GetValueOrDefault("en")?.Contains("tomestone", StringComparison.OrdinalIgnoreCase) == true);
        Assert.NotNull(tomestone);
        Assert.True(tomestone.StackSize > 0);
    }

    [SkippableFact]
    public void GetCurrencies_MultiLanguage_GilHasJapaneseName()
    {
        SkipIfNoGamePath();

        var response = BuildCurrenciesResponse(["en", "ja"]);

        var gil = response.Currencies.FirstOrDefault(c => c.RowId == 1);
        Assert.NotNull(gil);
        Assert.True(gil.Name.ContainsKey("ja"), "Gil should have a Japanese name.");
        Assert.False(string.IsNullOrWhiteSpace(gil.Name["ja"]));
    }

    // ── get_materia ────────────────────────────────────────────────────────

    [SkippableFact]
    public void GetMateria_NoFilter_ReturnsMateriaEntries()
    {
        SkipIfNoGamePath();

        var response = BuildMateriaResponse(null, null);

        Assert.True(response.TotalMatches > 0, "Expected at least one materia entry.");
        Assert.All(response.Materia, m =>
        {
            Assert.False(string.IsNullOrWhiteSpace(m.Stat), "Stat name should not be empty.");
            Assert.True(m.Tier >= 1 && m.Tier <= 12, $"Tier {m.Tier} out of range.");
            Assert.True(m.Name.ContainsKey("en"), "Entry should have English name.");
        });
    }

    [SkippableFact]
    public void GetMateria_StatFilter_ReturnsCritHitMateria()
    {
        SkipIfNoGamePath();

        var response = BuildMateriaResponse(null, "Critical Hit");

        Assert.True(response.TotalMatches > 0, "Expected Critical Hit materia.");
        Assert.All(response.Materia, m =>
            Assert.Contains("Critical Hit", m.Stat, StringComparison.OrdinalIgnoreCase));
    }

    [SkippableFact]
    public void GetMateria_StatFilter_SortedByTier()
    {
        SkipIfNoGamePath();

        var response = BuildMateriaResponse(null, "Critical Hit", limit: 200);

        var tiers = response.Materia.Select(m => m.Tier).ToList();
        Assert.Equal(tiers.OrderBy(t => t).ToList(), tiers);
    }

    [SkippableFact]
    public void GetMateria_QueryFilter_FiltersToMatchingNames()
    {
        SkipIfNoGamePath();

        var response = BuildMateriaResponse("Savage Aim", null);

        Assert.True(response.TotalMatches > 0, "Expected Savage Aim materia.");
        Assert.All(response.Materia, m =>
            Assert.Contains("Savage Aim", m.Name["en"], StringComparison.OrdinalIgnoreCase));
    }

    [SkippableFact]
    public void GetMateria_MultiLanguage_HasJapaneseNames()
    {
        SkipIfNoGamePath();

        var response = BuildMateriaResponse(null, "Critical Hit", langs: ["en", "ja"]);

        Assert.All(response.Materia, m =>
        {
            Assert.True(m.Name.ContainsKey("ja"), "Entry should have Japanese name.");
            Assert.False(string.IsNullOrWhiteSpace(m.Name["ja"]));
        });
    }

    [SkippableFact]
    public void GetMateria_Pagination_WorksCorrectly()
    {
        SkipIfNoGamePath();

        var page1 = BuildMateriaResponse(null, null, limit: 5, offset: 0);
        var page2 = BuildMateriaResponse(null, null, limit: 5, offset: 5);

        Assert.Equal(5, page1.Materia.Length);
        Assert.Equal(5, page2.Materia.Length);
        Assert.Empty(page1.Materia.Select(m => m.RowId).Intersect(page2.Materia.Select(m => m.RowId)));
    }

    // ── Localized string correctness ──────────────────────────────────────

    [SkippableTheory]
    [InlineData("en", "Warrior")]
    [InlineData("ja", "\u6226\u58eb")]
    [InlineData("de", "Krieger")]
    [InlineData("fr", "Guerrier")]
    public void GetJobs_KnownJobName_CorrectInLanguage(string lang, string expectedName)
    {
        SkipIfNoGamePath();

        var response = BuildJobsResponse([lang]);
        var found    = response.Jobs.Any(j =>
            j.Name.TryGetValue(lang, out var name) &&
            name.Equals(expectedName, StringComparison.OrdinalIgnoreCase));

        Assert.True(found, $"Expected job named '{expectedName}' in language '{lang}'.");
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions DeserializeOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private JobsResponse BuildJobsResponse(string[] langs)
    {
        var json = Tools.GetJobs(string.Join(",", langs));
        return JsonSerializer.Deserialize<JobsResponse>(json, DeserializeOpts)!;
    }

    private DutiesResponse BuildDutiesResponse(string? category, string[] langs)
    {
        var json = Tools.GetDuties(category, string.Join(",", langs));
        return JsonSerializer.Deserialize<DutiesResponse>(json, DeserializeOpts)!;
    }

    private MountsResponse BuildMountsResponse(
        string? query, string[]? langs = null, int? limit = null, int? offset = null)
    {
        var json = Tools.GetMounts(query, limit, offset,
            langs is null ? null : string.Join(",", langs));
        return JsonSerializer.Deserialize<MountsResponse>(json, DeserializeOpts)!;
    }

    private MinionsResponse BuildMinionsResponse(
        string? query, string[]? langs = null, int? limit = null, int? offset = null)
    {
        var json = Tools.GetMinions(query, limit, offset,
            langs is null ? null : string.Join(",", langs));
        return JsonSerializer.Deserialize<MinionsResponse>(json, DeserializeOpts)!;
    }

    private AchievementsResponse BuildAchievementsResponse(
        string? query, string[]? langs = null, int? limit = null, int? offset = null)
    {
        var json = Tools.GetAchievements(query, limit, offset,
            langs is null ? null : string.Join(",", langs));
        return JsonSerializer.Deserialize<AchievementsResponse>(json, DeserializeOpts)!;
    }

    private TraitsResponse BuildTraitsResponse(
        string? query, int? classJobId,
        string[]? langs = null, int? limit = null, int? offset = null)
    {
        var json = Tools.GetTraits(query, classJobId, limit, offset,
            langs is null ? null : string.Join(",", langs));
        return JsonSerializer.Deserialize<TraitsResponse>(json, DeserializeOpts)!;
    }

    private StatusesResponse BuildStatusesResponse(
        string? query, string? category,
        string[]? langs = null, int? limit = null, int? offset = null)
    {
        var json = Tools.GetStatuses(query, category, limit, offset,
            langs is null ? null : string.Join(",", langs));
        return JsonSerializer.Deserialize<StatusesResponse>(json, DeserializeOpts)!;
    }

    private ActionsResponse BuildActionsResponse(
        string? query, int? classJobId,
        string[]? langs = null, int? limit = null, int? offset = null)
    {
        var json = Tools.GetActions(query, classJobId, limit, offset,
            langs is null ? null : string.Join(",", langs));
        return JsonSerializer.Deserialize<ActionsResponse>(json, DeserializeOpts)!;
    }

    private RacesResponse BuildRacesResponse(string[]? langs = null)
    {
        var json = Tools.GetRaces(langs is null ? null : string.Join(",", langs));
        return JsonSerializer.Deserialize<RacesResponse>(json, DeserializeOpts)!;
    }

    private WorldsResponse BuildWorldsResponse(string? query = null)
    {
        var json = Tools.GetWorlds(query);
        return JsonSerializer.Deserialize<WorldsResponse>(json, DeserializeOpts)!;
    }

    private WeatherResponse BuildWeatherResponse(
        string? query, string[]? langs = null, int? limit = null, int? offset = null)
    {
        var json = Tools.GetWeather(query, limit, offset,
            langs is null ? null : string.Join(",", langs));
        return JsonSerializer.Deserialize<WeatherResponse>(json, DeserializeOpts)!;
    }

    private TitlesResponse BuildTitlesResponse(
        string? query, string[]? langs = null, int? limit = null, int? offset = null)
    {
        var json = Tools.GetTitles(query, limit, offset,
            langs is null ? null : string.Join(",", langs));
        return JsonSerializer.Deserialize<TitlesResponse>(json, DeserializeOpts)!;
    }

    private CurrenciesResponse BuildCurrenciesResponse(string[]? langs = null)
    {
        var json = Tools.GetCurrencies(langs is null ? null : string.Join(",", langs));
        return JsonSerializer.Deserialize<CurrenciesResponse>(json, DeserializeOpts)!;
    }

    private MateriaResponse BuildMateriaResponse(
        string? query, string? stat,
        string[]? langs = null, int? limit = null, int? offset = null)
    {
        var json = Tools.GetMateria(query, stat, limit, offset,
            langs is null ? null : string.Join(",", langs));
        return JsonSerializer.Deserialize<MateriaResponse>(json, DeserializeOpts)!;
    }

    // ── health / list_languages ────────────────────────────────────────────

    [SkippableFact]
    public void Health_ReturnsOkStatus()
    {
        SkipIfNoGamePath();

        var health = new HealthTool(GameData, Cache);
        var json   = health.Health();

        var response = JsonSerializer.Deserialize<HealthResponse>(json, DeserializeOpts)!;
        Assert.Equal("ok", response.Status);
        Assert.False(string.IsNullOrWhiteSpace(response.DetectedVersion),
            "DetectedVersion should be populated.");
        Assert.False(string.IsNullOrWhiteSpace(response.GamePath),
            "GamePath should be populated.");
        Assert.True(response.UptimeSeconds >= 0, "UptimeSeconds should be non-negative.");
    }

    [SkippableFact]
    public void ListLanguages_ContainsEnglish()
    {
        SkipIfNoGamePath();

        var health = new HealthTool(GameData, Cache);
        var json   = health.ListLanguages();

        var response = JsonSerializer.Deserialize<LanguagesResponse>(json, DeserializeOpts)!;
        Assert.NotEmpty(response.Languages);

        var en = response.Languages.FirstOrDefault(l => l.Code == "en");
        Assert.NotNull(en);
        Assert.True(en.Available, "English should be available in any FFXIV installation.");
        Assert.Equal("English", en.DisplayName);
    }
}
