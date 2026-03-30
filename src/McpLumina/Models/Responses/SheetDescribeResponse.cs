namespace McpLumina.Models.Responses;

public sealed record SheetDescribeResponse : BaseResponse
{
    public string         Sheet          { get; init; } = string.Empty;
    public int            RowCountApprox { get; init; }
    public ColumnInfo[]   Columns        { get; init; } = [];
    public string[]       Languages      { get; init; } = [];
    public SchemaInfo?    Schema         { get; init; }
}

public sealed record ColumnInfo(
    int    Index,
    string Name,   // "Column_{index}" for unnamed columns
    string Type    // human-readable type string per spec mapping
);

/// <summary>
/// Schema availability and accuracy note included in describe_sheet responses.
/// </summary>
public sealed record SchemaInfo(
    bool    Available,
    string? Version,
    string? Note
);
