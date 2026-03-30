using System.ComponentModel;
using McpLumina.Constants;
using McpLumina.Services;
using McpLumina.Validators;
using ModelContextProtocol.Server;

namespace McpLumina.Tools;

[McpServerToolType]
public sealed class SearchTool(GameDataService gameData)
{
    [McpServerTool(Name = "search_rows")]
    [Description(
        "Searches rows in a sheet for a query string across string columns. " +
        "This is a full O(n) scan of the entire sheet. " +
        "Use text_fields to restrict the text search to specific columns (accepts field names or Column_N). " +
        "Use column_filters to pre-filter by exact integer column values before the text match " +
        "(e.g. 'ClassJob=24' or 'Column_10=24'). " +
        "Use return_fields to limit returned column data and reduce token usage. " +
        "Use limit/offset for pagination (max limit=200, max offset=10000).")]
    public string SearchRows(
        [Description("Sheet name, e.g. 'Action', 'Item'.")] string sheet,
        [Description("Text to search for (case-insensitive substring match).")] string query,
        [Description("Comma-separated field names to search, e.g. 'Name' or 'Column_0'. Empty = search all string columns.")] string? textFields = null,
        [Description("Comma-separated language codes. Defaults to server default.")] string? languages = null,
        [Description("Max results to return (1–200). Default 50.")] int? limit = null,
        [Description("Result offset for pagination (0–10000). Default 0.")] int? offset = null,
        [Description("Comma-separated column filters, e.g. 'ClassJob=24,Column_12=2'. Filters by exact integer value before text matching.")] string? columnFilters = null,
        [Description("Comma-separated field names to include in results, e.g. 'Name,ClassJob'. Empty = all fields.")] string? returnFields = null) =>
        ToolHelper.Execute(() =>
        {
            InputValidator.ValidateSheetName(sheet);
            InputValidator.ValidateSearchQuery(query);

            var validLimit    = InputValidator.ValidateLimit(limit);
            var validOffset   = InputValidator.ValidateOffset(offset);
            var langs         = gameData.Languages.Resolve(InputValidator.ParseLanguages(languages));
            var textFieldsArr = InputValidator.ParseTextFields(textFields);
            var filters       = InputValidator.ParseColumnFilters(columnFilters);
            var returnFieldsArr = InputValidator.ParseReturnFields(returnFields);

            var result = gameData.SearchRows(
                sheetName:    sheet,
                query:        query,
                textFields:   textFieldsArr,
                rawFilters:   filters,
                languages:    langs,
                limit:        validLimit,
                offset:       validOffset,
                returnFields: returnFieldsArr);

            return ToolHelper.Ok(result);
        });
}
