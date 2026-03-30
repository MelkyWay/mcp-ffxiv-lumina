using Xunit;

namespace McpLumina.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class SheetToolsIntegrationTests : IntegrationTestBase
{
    [SkippableFact]
    public void ListSheets_ReturnsNonEmptyList()
    {
        SkipIfNoGamePath();
        var response = GameData.GetSheetList();
        Assert.NotEmpty(response.Sheets);
        Assert.True(response.Count > 100, $"Expected > 100 sheets, got {response.Count}.");
    }

    [SkippableFact]
    public void ListSheets_ContainsKnownSheets()
    {
        SkipIfNoGamePath();
        var response = GameData.GetSheetList();
        Assert.Contains("Action",                  response.Sheets);
        Assert.Contains("Item",                    response.Sheets);
        Assert.Contains("ClassJob",                response.Sheets);
        Assert.Contains("ContentFinderCondition",  response.Sheets);
    }

    [SkippableFact]
    public void DescribeSheet_ClassJob_ReturnsColumnsAndRowCount()
    {
        SkipIfNoGamePath();
        var response = GameData.DescribeSheet("ClassJob");
        Assert.Equal("ClassJob", response.Sheet);
        Assert.NotEmpty(response.Columns);
        Assert.True(response.RowCountApprox > 0);
    }

    [SkippableFact]
    public void DescribeSheet_UnknownSheet_ThrowsSheetNotFound()
    {
        SkipIfNoGamePath();
        Assert.Throws<McpLumina.Models.SheetNotFoundException>(
            () => GameData.DescribeSheet("DoesNotExistSheet_XYZ"));
    }

    [SkippableFact]
    public void GetRow_ClassJobRow2_ReturnsValidData()
    {
        SkipIfNoGamePath();
        // ClassJob row 2 is Pugilist (base class for Monk)
        var response = GameData.GetRow("ClassJob", 2, ["en"]);
        Assert.Equal(2u, response.RowId);
        Assert.Equal("ClassJob", response.Sheet);
        Assert.NotEmpty(response.Fields);
    }

    [SkippableFact]
    public void GetRow_MissingRow_ThrowsRowNotFound()
    {
        SkipIfNoGamePath();
        Assert.Throws<McpLumina.Models.RowNotFoundException>(
            () => GameData.GetRow("ClassJob", 99999u, ["en"]));
    }

    [SkippableFact]
    public void GetRows_MultipleIds_ReturnsBatch()
    {
        SkipIfNoGamePath();
        var response = GameData.GetRows("ClassJob", [2u, 3u, 4u], ["en"]);
        Assert.Equal(3, response.Rows.Length);
        Assert.Empty(response.MissingRowIds);
    }

    [SkippableFact]
    public void SearchRows_QueryWarrior_ReturnsResults()
    {
        SkipIfNoGamePath();
        var response = GameData.SearchRows(
            sheetName:  "ClassJob",
            query:         "warrior",
            textFields:    [],
            rawFilters:    [],
            languages:     ["en"],
            limit:         10,
            offset:        0);

        Assert.True(response.TotalMatches > 0, "Expected at least one match for 'warrior'.");
    }

    [SkippableFact]
    public void SearchRows_NoMatch_ReturnsEmpty()
    {
        SkipIfNoGamePath();
        var response = GameData.SearchRows(
            sheetName:  "ClassJob",
            query:         "zzzzXXXX_nomatch_9999",
            textFields:    [],
            rawFilters:    [],
            languages:     ["en"],
            limit:         10,
            offset:        0);

        Assert.Equal(0, response.TotalMatches);
    }
}
