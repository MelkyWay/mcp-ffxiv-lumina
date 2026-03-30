namespace McpLumina.Models.Responses;

public sealed record RowResponse : BaseResponse
{
    public string   Sheet              { get; init; } = string.Empty;
    public uint     RowId              { get; init; }
    public string[] LanguagesRequested { get; init; } = [];
    public string[] LanguagesReturned  { get; init; } = [];
    public bool     FallbackUsed       { get; init; }

    /// <summary>
    /// Column values keyed by column name (or "Column_{index}" for unnamed columns).
    /// For localized string columns, the value is a dictionary of language → string.
    /// For non-string columns, the value is the scalar C# type (int, bool, float, etc.).
    /// </summary>
    public Dictionary<string, object?> Fields { get; init; } = [];
}

public sealed record RowsResponse : BaseResponse
{
    public string       Sheet              { get; init; } = string.Empty;
    public uint[]       RowIds             { get; init; } = [];
    public string[]     LanguagesRequested { get; init; } = [];
    public string[]     LanguagesReturned  { get; init; } = [];
    public bool         FallbackUsed       { get; init; }
    public RowResponse[] Rows              { get; init; } = [];
    public uint[]       MissingRowIds      { get; init; } = [];
}
