using System.Text.Json;
using McpLumina.Constants;
using McpLumina.Models.Responses;
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
}
