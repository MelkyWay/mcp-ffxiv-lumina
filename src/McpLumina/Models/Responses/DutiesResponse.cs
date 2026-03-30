namespace McpLumina.Models.Responses;

public sealed record DutiesResponse : BaseResponse
{
    public string?      Category           { get; init; }
    public string[]     LanguagesRequested { get; init; } = [];
    public string[]     LanguagesReturned  { get; init; } = [];
    public bool         FallbackUsed       { get; init; }
    public int          Count              { get; init; }
    public DutyEntry[]  Duties             { get; init; } = [];
}

public sealed record DutyEntry(
    uint   RowId,
    /// <summary>Localized duty name keyed by language code.</summary>
    Dictionary<string, string> Name,
    string Category,           // one of DutyCategories constants
    bool   IsHighEndDuty,      // Ultimate flag
    byte   LevelRequired,
    ushort ItemLevelRequired
);
