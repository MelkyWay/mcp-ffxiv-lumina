namespace McpLumina.Models.Responses;

public sealed record LabelsResponse : BaseResponse
{
    public string       Kind               { get; init; } = string.Empty;
    public string[]     LanguagesRequested { get; init; } = [];
    public string[]     LanguagesReturned  { get; init; } = [];
    public bool         FallbackUsed       { get; init; }
    public LabelEntry[] Labels             { get; init; } = [];
}

public sealed record LabelEntry(
    uint   RowId,
    Dictionary<string, string> Name,
    string? Note = null  // e.g. "derived" for role labels not sourced from game strings
);
