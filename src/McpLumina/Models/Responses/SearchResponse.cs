namespace McpLumina.Models.Responses;

public sealed record SearchResponse : BaseResponse
{
    public string       Sheet              { get; init; } = string.Empty;
    public string       Query              { get; init; } = string.Empty;
    public string[]     LanguagesRequested { get; init; } = [];
    public string[]     LanguagesReturned  { get; init; } = [];
    public bool         FallbackUsed       { get; init; }
    public Dictionary<string, long>? ColumnFilters { get; init; }
    public int          TotalMatches       { get; init; }
    public int          Offset             { get; init; }
    public int          Limit              { get; init; }
    public int          RowsScanned        { get; init; }
    public RowResponse[] Results           { get; init; } = [];
}
