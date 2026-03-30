using System.ComponentModel;
using McpLumina.Models;
using McpLumina.Services;
using McpLumina.Validators;
using ModelContextProtocol.Server;

namespace McpLumina.Tools;

[McpServerToolType]
public sealed class SheetTools(GameDataService gameData, ResponseCacheService cache)
{
    [McpServerTool(Name = "list_sheets")]
    [Description(
        "Returns the names of all FFXIV game data sheets available in this installation. " +
        "Parsed from exd/root.exl at startup and cached for the server lifetime. " +
        "Sheet names can be used as the 'sheet' parameter in other tools.")]
    public string ListSheets() =>
        ToolHelper.Execute(() =>
            cache.GetOrCreate("list_sheets", () => ToolHelper.Ok(gameData.GetSheetList())));

    [McpServerTool(Name = "describe_sheet")]
    [Description(
        "Returns column metadata and approximate row count for the named sheet. " +
        "When EXDSchema is configured, columns are reported with their real field names (e.g. 'Name', 'ClassJob'). " +
        "Otherwise columns are reported as 'Column_{index}'. " +
        "The response includes a 'schema' object with an accuracy note: EXDSchema names are best-effort and " +
        "may not match binary column order for complex sheets (arrays, mixed types). " +
        "Column_N syntax always resolves correctly regardless of schema. " +
        "Use this before get_row or search_rows to understand available fields.")]
    public string DescribeSheet(
        [Description("Sheet name, e.g. 'Action', 'Item', 'ClassJob'. Case-sensitive.")] string sheet) =>
        ToolHelper.Execute(() =>
        {
            InputValidator.ValidateSheetName(sheet);
            return cache.GetOrCreate($"describe:{sheet}", () => ToolHelper.Ok(gameData.DescribeSheet(sheet)));
        });

    [McpServerTool(Name = "get_row")]
    [Description(
        "Returns column values for a single row from the named sheet. " +
        "String columns are returned as a dictionary keyed by language code when multiple languages are requested. " +
        "Non-string columns return scalar values (int, bool, float, etc.). " +
        "Use return_fields to limit the response to specific columns and reduce token usage.")]
    public string GetRow(
        [Description("Sheet name, e.g. 'Action'.")] string sheet,
        [Description("Row ID (unsigned integer).")] uint rowId,
        [Description("Comma-separated language codes to return, e.g. 'en,fr'. Defaults to server default language.")] string? languages = null,
        [Description("Comma-separated field names to include, e.g. 'Name,ClassJob' or 'Column_0,Column_10'. Empty = all fields.")] string? returnFields = null) =>
        ToolHelper.Execute(() =>
        {
            InputValidator.ValidateSheetName(sheet);
            var langs  = gameData.Languages.Resolve(InputValidator.ParseLanguages(languages));
            var fields = InputValidator.ParseReturnFields(returnFields);
            return ToolHelper.Ok(gameData.GetRow(sheet, rowId, langs, fields));
        });

    [McpServerTool(Name = "get_rows")]
    [Description(
        "Batched version of get_row. Returns multiple rows in a single call. " +
        "Maximum 100 row IDs per call. Missing row IDs are reported in missingRowIds. " +
        "Use return_fields to limit the response to specific columns and reduce token usage.")]
    public string GetRows(
        [Description("Sheet name.")] string sheet,
        [Description("Array of row IDs, e.g. [1, 2, 3].")] uint[] rowIds,
        [Description("Comma-separated language codes. Defaults to server default.")] string? languages = null,
        [Description("Comma-separated field names to include, e.g. 'Name,ClassJob' or 'Column_0,Column_10'. Empty = all fields.")] string? returnFields = null) =>
        ToolHelper.Execute(() =>
        {
            InputValidator.ValidateSheetName(sheet);
            InputValidator.ValidateBatchSize(rowIds);
            var langs  = gameData.Languages.Resolve(InputValidator.ParseLanguages(languages));
            var fields = InputValidator.ParseReturnFields(returnFields);
            return ToolHelper.Ok(gameData.GetRows(sheet, rowIds, langs, fields));
        });
}
