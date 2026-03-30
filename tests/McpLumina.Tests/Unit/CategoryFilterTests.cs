using McpLumina.Constants;
using Xunit;

namespace McpLumina.Tests.Unit;

public sealed class CategoryFilterTests
{
    [Theory]
    [InlineData("dungeon",   ContentTypeIds.Dungeon)]
    [InlineData("trial",     ContentTypeIds.Trial)]
    [InlineData("raid",      ContentTypeIds.Raid)]
    [InlineData("ultimate",  ContentTypeIds.Ultimate)]
    [InlineData("criterion", ContentTypeIds.Criterion)]
    [InlineData("unreal",    ContentTypeIds.Trial)]
    public void ToContentTypeId_KnownCategory_ReturnsMappedId(string category, int expected)
    {
        var result = DutyCategories.ToContentTypeId(category);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ToContentTypeId_Null_ReturnsNull()
    {
        Assert.Null(DutyCategories.ToContentTypeId(null!));
    }

    [Fact]
    public void ToContentTypeId_Unknown_ReturnsNull()
    {
        Assert.Null(DutyCategories.ToContentTypeId("pvp"));
    }

    [Theory]
    [InlineData("DUNGEON")]
    [InlineData("Dungeon")]
    [InlineData("dungeon")]
    public void ToContentTypeId_CaseInsensitive(string category)
    {
        Assert.Equal(ContentTypeIds.Dungeon, DutyCategories.ToContentTypeId(category));
    }

    [Fact]
    public void AllCategories_ContainsExpectedValues()
    {
        Assert.Contains("dungeon",   DutyCategories.All);
        Assert.Contains("trial",     DutyCategories.All);
        Assert.Contains("raid",      DutyCategories.All);
        Assert.Contains("ultimate",  DutyCategories.All);
        Assert.Contains("criterion", DutyCategories.All);
        Assert.Contains("unreal",    DutyCategories.All);
    }

    [Fact]
    public void RoleLabels_LimitedResolvesToLimitedLabel()
    {
        var label = RoleLabels.Resolve(roleId: 0, isLimited: true, rowId: 0);
        Assert.Equal(RoleLabels.Limited, label);
    }

    [Theory]
    [InlineData(1, "Tank")]
    [InlineData(2, "Melee DPS")]
    [InlineData(3, "Physical Ranged DPS")]  // rowId=0 is not a magical ranged job
    [InlineData(4, "Healer")]
    [InlineData(5, "Magical Ranged DPS")]   // ByRoleId fallback for label enumeration
    public void RoleLabels_KnownRoleId_ReturnsLabel(byte roleId, string expected)
    {
        var label = RoleLabels.Resolve(roleId, isLimited: false, rowId: 0);
        Assert.Equal(expected, label);
    }

    [Theory]
    [InlineData(7)]   // Thaumaturge
    [InlineData(25)]  // Black Mage
    [InlineData(26)]  // Arcanist
    [InlineData(27)]  // Summoner
    [InlineData(35)]  // Red Mage
    [InlineData(42)]  // Pictomancer
    public void RoleLabels_MagicalRangedJobIds_ReturnMagicalRangedDps(uint rowId)
    {
        var label = RoleLabels.Resolve(roleId: 3, isLimited: false, rowId);
        Assert.Equal(RoleLabels.MagicalRanged, label);
    }

    [Fact]
    public void RoleLabels_PhysicalRangedJobId_ReturnsPhysicalRangedDps()
    {
        // Archer (rowId=5) has Role=3 but is not magical ranged
        var label = RoleLabels.Resolve(roleId: 3, isLimited: false, rowId: 5);
        Assert.Equal("Physical Ranged DPS", label);
    }

    [Fact]
    public void RoleLabels_UnknownRoleId_ReturnsNone()
    {
        var label = RoleLabels.Resolve(roleId: 99, isLimited: false, rowId: 0);
        Assert.Equal(RoleLabels.None, label);
    }
}
