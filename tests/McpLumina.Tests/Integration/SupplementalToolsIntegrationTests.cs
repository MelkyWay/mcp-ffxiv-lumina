using System.Text.Json;
using McpLumina.Models.Responses;
using McpLumina.Services;
using McpLumina.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace McpLumina.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class SupplementalToolsIntegrationTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions DeserializeOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly SupplementalDataService? _supplemental;
    private readonly SupplementalTools?       _supplementalTools;

    public SupplementalToolsIntegrationTests()
    {
        if (ShouldSkip) return;

        _supplemental      = new SupplementalDataService(GameData, NullLogger<SupplementalDataService>.Instance);
        _supplementalTools = new SupplementalTools(_supplemental, GameData);
    }

    // ── get_mob_drops ──────────────────────────────────────────────────────

    [SkippableFact]
    public void GetMobDrops_NoFilter_ReturnsNonEmpty()
    {
        SkipIfNoGamePath();

        var response = BuildMobDropsResponse(null, null);

        Assert.True(response.TotalMatches > 0, "Expected mob drop rows.");
        Assert.All(response.Drops, d =>
        {
            Assert.True(d.BNpcNameId > 0);
            Assert.True(d.ItemId     > 0);
            Assert.True(d.MobName.ContainsKey("en"),  "Each entry should have an English mob name.");
            Assert.True(d.ItemName.ContainsKey("en"), "Each entry should have an English item name.");
        });
    }

    [SkippableFact]
    public void GetMobDrops_NoFilter_TotalMatchesAbove2000()
    {
        SkipIfNoGamePath();

        // The dataset has 2,644 rows but some won't resolve names — expect a large majority.
        var response = BuildMobDropsResponse(null, null, limit: 1, offset: 0);

        Assert.True(response.TotalMatches > 2000,
            $"Expected >2000 mob drop entries, got {response.TotalMatches}");
    }

    [SkippableFact]
    public void GetMobDrops_MonsterQueryBehemoth_ReturnsMatches()
    {
        SkipIfNoGamePath();

        var response = BuildMobDropsResponse("Behemoth", null, limit: 50);

        Assert.True(response.TotalMatches > 0, "Expected Behemoth drops.");
        Assert.All(response.Drops, d =>
            Assert.Contains("Behemoth", d.MobName["en"], StringComparison.OrdinalIgnoreCase));
    }

    [SkippableFact]
    public void GetMobDrops_ItemQueryHide_ReturnsHideDrops()
    {
        SkipIfNoGamePath();

        var response = BuildMobDropsResponse(null, "hide", limit: 50);

        Assert.True(response.TotalMatches > 0, "Expected items containing 'hide'.");
        Assert.All(response.Drops, d =>
            Assert.Contains("hide", d.ItemName["en"], StringComparison.OrdinalIgnoreCase));
    }

    [SkippableFact]
    public void GetMobDrops_BothFilters_NarrowsResults()
    {
        SkipIfNoGamePath();

        var monsterOnly = BuildMobDropsResponse("Behemoth", null);
        var both        = BuildMobDropsResponse("Behemoth", "hide");

        Assert.True(both.TotalMatches <= monsterOnly.TotalMatches,
            "Adding itemQuery should not increase result count.");
    }

    [SkippableFact]
    public void GetMobDrops_Pagination_OffsetAdvancesResults()
    {
        SkipIfNoGamePath();

        var page0 = BuildMobDropsResponse(null, null, limit: 10, offset: 0);
        var page1 = BuildMobDropsResponse(null, null, limit: 10, offset: 10);

        Assert.Equal(10, page0.Drops.Length);
        Assert.Equal(10, page1.Drops.Length);

        var ids0 = page0.Drops.Select(d => (d.BNpcNameId, d.ItemId)).ToHashSet();
        var ids1 = page1.Drops.Select(d => (d.BNpcNameId, d.ItemId)).ToHashSet();
        Assert.Empty(ids0.Intersect(ids1));
    }

    [SkippableFact]
    public void GetMobDrops_MultiLanguage_HasJapaneseNames()
    {
        SkipIfNoGamePath();

        var response = BuildMobDropsResponse("Behemoth", null, langs: ["en", "ja"]);

        Assert.True(response.Drops.Length > 0);
        Assert.All(response.Drops, d =>
        {
            Assert.True(d.MobName.ContainsKey("ja"),  $"BNpcNameId {d.BNpcNameId} missing Japanese mob name.");
            Assert.True(d.ItemName.ContainsKey("ja"), $"ItemId {d.ItemId} missing Japanese item name.");
            Assert.False(string.IsNullOrWhiteSpace(d.MobName["ja"]));
            Assert.False(string.IsNullOrWhiteSpace(d.ItemName["ja"]));
        });
    }

    [SkippableFact]
    public void GetMobDrops_TotalMatchesConsistentAcrossPages()
    {
        SkipIfNoGamePath();

        var page0 = BuildMobDropsResponse(null, null, limit: 10, offset: 0);
        var page1 = BuildMobDropsResponse(null, null, limit: 10, offset: 50);

        Assert.Equal(page0.TotalMatches, page1.TotalMatches);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private MobDropsResponse BuildMobDropsResponse(
        string? monsterQuery, string? itemQuery,
        string[]? langs = null, int? limit = null, int? offset = null)
    {
        var json = _supplementalTools!.GetMobDrops(
            monsterQuery, itemQuery, limit, offset,
            langs is null ? null : string.Join(",", langs));
        return JsonSerializer.Deserialize<MobDropsResponse>(json, DeserializeOpts)!;
    }
}
